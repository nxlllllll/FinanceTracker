using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Worker.CurrencyRate.Client;
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
	IOptionsMonitor<CurrencyRateJobOptions> options,
	ILogger<CurrencyRateJob> logger
) : IJob
{
	public async Task Execute(IJobExecutionContext executionContext)
	{
		if (!options.CurrentValue.IsEnabled)
		{
			logger.ZLogInformation(message: $"[{nameof(CurrencyRateJob)}] Disabled. Skipping.");
			return;
		}

		await ProcessAsync(ct: executionContext.CancellationToken);
	}

	private async Task ProcessAsync(CancellationToken ct)
	{
		IReadOnlyList<CurrencyInfo> currencies = await currencyReadRepository.GetAllActiveAsync(ct: ct);

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

				List<Core.ValueObjects.CurrencyRate> entries = response.ConversionRates
					.Where(predicate: rate => rate.Key != baseCurrency.Code && knownCodes.Contains(item: rate.Key))
					.Select(selector: kvp => Core.ValueObjects.CurrencyRate.Reconstitute(
						baseCurrency: Currency.Reconstitute(value: baseCurrency.Code),
						target: Currency.Reconstitute(value: kvp.Key),
						rate: kvp.Value,
						date: today
					)).ToList();

				await currencyRateWriteRepository.UpsertRatesAsync(rates: entries, ct: ct);

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
}