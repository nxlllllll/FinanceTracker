using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Worker.CurrencyRate.Client;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.CurrencyRate.Jobs;

[DisallowConcurrentExecution]
public sealed class CurrencyRateJob(
    ExchangeRateApiClient apiClient,
    ICurrencyReadRepository currencyReadRepository,
    ICurrencyRateWriteRepository currencyRateWriteRepository,
    IDateProvider dateProvider,
    ILogger<CurrencyRateJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext executionContext)
        => await ProcessAsync(ct: executionContext.CancellationToken);

    private async Task ProcessAsync(CancellationToken ct)
    {
        IReadOnlyList<CurrencyDto> currencies = await currencyReadRepository.GetAllActiveAsync(ct: ct);

        if (currencies.Count == 0)
        {
            logger.ZLogWarning(message: $"No active currencies found. Skipping rate fetch.");
            return;
        }

        HashSet<string> knownCodes = currencies.Select(selector: c => c.Code).ToHashSet();
        DateOnly today = dateProvider.UtcToday;

        logger.ZLogInformation(message: $"Fetching rates for {currencies.Count} currencies.");

        int totalUpserted = 0;
        int failed = 0;

        foreach (CurrencyDto baseCurrency in currencies)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                ExchangeRateApiResponse? response = await apiClient.GetRatesAsync(baseCurrency: baseCurrency.Code, ct: ct);

                if (response is null)
                {
                    failed++;
                    continue;
                }

                List<CurrencyRateDto> entries = response.ConversionRates
                    .Where(predicate: rate => rate.Key != baseCurrency.Code && knownCodes.Contains(item: rate.Key))
                    .Select(selector: kvp => new CurrencyRateDto(
                        Base: Currency.Reconstitute(value: baseCurrency.Code),
                        Target: Currency.Reconstitute(value: kvp.Key),
                        Rate: kvp.Value,
                        Date: today
                    )).ToList();

                await currencyRateWriteRepository.UpsertRatesAsync(rates: entries, ct: ct);

                totalUpserted += entries.Count;
                logger.ZLogInformation(message: $"Upserted {entries.Count} rates for {baseCurrency.Code}.");
            }
            catch (Exception ex)
            {
                failed++;
                logger.ZLogError(exception: ex, message: $"Failed to process rates for {baseCurrency.Code}.");
            }
        }

        logger.ZLogInformation(message: $"Completed. Total upserted: {totalUpserted}, failed currencies: {failed}.");
    }
}