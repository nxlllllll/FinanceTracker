using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Worker.CurrencyRate.Client;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.CurrencyRate.Job;

[DisallowConcurrentExecution]
public sealed class CurrencyRateJob(
	ExchangeRateApiClient apiClient,
	ICurrencyReadRepository currencyReadRepository,
	ICurrencyRateWriteRepository currencyRateWriteRepository,
	IDateProvider dateProvider,
	IUnitOfWork unitOfWork,
	IOptionsMonitor<CurrencyRateJobOptions> options,
	ILogger<CurrencyRateJob> logger
) : BaseJob<CurrencyRateJobOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(CurrencyRateJobOptions options, CancellationToken ct)
	{
		IReadOnlyList<CurrencyInfo> currencies = await currencyReadRepository.GetAllActiveAsync(ct: ct);

		if (currencies.Count == 0)
		{
			logger.ZLogInformation(message: $"No active currencies configured. Nothing to fetch.");
			return;
		}

		HashSet<string> knownCodes = currencies.Select(selector: currency => currency.Code).ToHashSet();
		DateOnly today = DateOnly.FromDateTime(dateTime: dateProvider.UtcNow.UtcDateTime);

		int totalUpserted = 0;
		int failed = 0;

		foreach (CurrencyInfo baseCurrency in currencies)
		{
			if (ct.IsCancellationRequested)
				break;

			try
			{
				ExchangeRateApiResponse? response = await apiClient.GetRatesAsync(baseCurrency: baseCurrency.Code, ct: ct);

				if (response is null)
				{
					failed++;
					WorkerMetrics.CurrencyRatesFetchFailed.Add(delta: 1, new KeyValuePair<string, object?>(key: "base_currency", value: baseCurrency.Code));
					continue;
				}

				List<Core.ValueObjects.CurrencyRate> entries = BuildEntries(
					response: response,
					baseCurrency: baseCurrency,
					knownCodes: knownCodes,
					today: today
				);

				if (entries.Count == 0)
				{
					logger.ZLogWarning(message: $"No usable rates for {baseCurrency.Code}. Nothing to upsert.");
					continue;
				}

				await unitOfWork.ExecuteInTransactionAsync(
					operation: async () => await currencyRateWriteRepository.UpsertRatesAsync(rates: entries, ct: ct),
					ct: ct
				);

				totalUpserted += entries.Count;
				WorkerMetrics.CurrencyRatesUpserted.Add(delta: entries.Count, new KeyValuePair<string, object?>(key: "base_currency", value: baseCurrency.Code));

				logger.ZLogInformation(message: $"Upserted {entries.Count} rates for {baseCurrency.Code}.");
			}
			catch (Exception ex)
			{
				failed++;
				WorkerMetrics.CurrencyRatesFetchFailed.Add(delta: 1, new KeyValuePair<string, object?>(key: "base_currency", value: baseCurrency.Code));
				logger.ZLogError(exception: ex, message: $"Failed to process rates for {baseCurrency.Code}.");
			}
		}

		logger.ZLogInformation(message: $"Completed. Total upserted: {totalUpserted}, failed currencies: {failed}.");
	}

	private List<Core.ValueObjects.CurrencyRate> BuildEntries(
		ExchangeRateApiResponse response,
		CurrencyInfo baseCurrency,
		HashSet<string> knownCodes,
		DateOnly today)
	{
		List<Core.ValueObjects.CurrencyRate> entries = new List<Core.ValueObjects.CurrencyRate>(capacity: response.ConversionRates.Count);

		foreach ((string targetCode, decimal rawRate) in response.ConversionRates)
		{
			if (targetCode == baseCurrency.Code || !knownCodes.Contains(item: targetCode))
				continue;

			Result<Core.ValueObjects.CurrencyRate, DomainException> created = Core.ValueObjects.CurrencyRate.Create(
				baseCurrency: Currency.Reconstitute(value: baseCurrency.Code),
				target: Currency.Reconstitute(value: targetCode),
				rate: rawRate,
				date: today
			);

			if (created.IsFailure)
			{
				WorkerMetrics.CurrencyRatesRejected.Add(delta: 1, new KeyValuePair<string, object?>(key: "base_currency", value: baseCurrency.Code));
				logger.ZLogWarning(message: $"Discarded {baseCurrency.Code}/{targetCode}: the provider sent {rawRate}, which cannot be stored as a rate.");
				continue;
			}

			Core.ValueObjects.CurrencyRate rate = created.Value;

			if (rate.Rate != rawRate)
			{
				WorkerMetrics.CurrencyRatesNormalized.Add(delta: 1, new KeyValuePair<string, object?>(key: "base_currency", value: baseCurrency.Code));
				logger.ZLogWarning(message: $"Rounded {baseCurrency.Code}/{targetCode} from {rawRate} to {rate.Rate}: the provider sends more precision than numeric(18, 6) keeps.");
			}

			entries.Add(item: rate);
		}

		return entries;
	}
}
