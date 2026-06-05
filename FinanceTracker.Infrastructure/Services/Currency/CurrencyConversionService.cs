using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Services.Currency;

public sealed class CurrencyConversionService(
	ICurrencyRateReadRepository currencyRateReadRepository,
	ILogger<CurrencyConversionService> logger
) : ICurrencyConversionService
{
	public async Task<ConversionResult> GetConversionRateAsync(
		Core.ValueObjects.Currency fromCurrency,
		Core.ValueObjects.Currency toCurrency,
		DateOnly date,
		CancellationToken ct = default)
	{
		if (fromCurrency == toCurrency)
			return new ConversionResult(Rate: 1m, IsPending: false);

		decimal? exactRate = await currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: fromCurrency,
			targetCurrencyCode: toCurrency,
			date: date,
			ct: ct
		);

		if (exactRate is not null)
			return new ConversionResult(Rate: exactRate.Value, IsPending: false);

		logger.ZLogWarning(message: $"No exact rate for {fromCurrency} > {toCurrency} on {date:dd.MM.yyyy}, using latest available.");

		decimal? latestRate = await currencyRateReadRepository.GetLatestRateAsync(
			baseCurrencyCode: fromCurrency,
			targetCurrencyCode: toCurrency,
			ct: ct
		);

		if (latestRate is not null)
			return new ConversionResult(Rate: latestRate.Value, IsPending: true);

		logger.ZLogError(message: $"No exchange rate found for {fromCurrency} > {toCurrency}.");
		throw new CurrencyRateNotFoundException(
			message: $"The exchange rate for {fromCurrency} > {toCurrency} was not found.",
			fromCurrency: fromCurrency,
			toCurrency: toCurrency
		);
	}

	public async Task<Dictionary<CurrencyRateRequest, ConversionResult>> GetConversionRatesBatchAsync(
		IReadOnlyCollection<CurrencyRateRequest> requests,
		CancellationToken ct = default)
	{
		if (requests.Count == 0)
			return [];

		Dictionary<CurrencyRateRequest, ConversionResult> result = [];

		List<CurrencyRateRequest> foreignRequests = requests.Where(predicate: r => r.From != r.To).ToList();

		foreach (CurrencyRateRequest request in requests.Where(predicate: r => r.From == r.To))
			result[request] = new ConversionResult(Rate: 1m, IsPending: false);

		if (foreignRequests.Count == 0)
			return result;

		Dictionary<CurrencyRateRequest, decimal> exactRates = await currencyRateReadRepository.GetRatesBatchAsync(requests: foreignRequests, ct: ct);

		List<CurrencyLatestRateRequest> missingPairs = [];

		foreach (CurrencyRateRequest request in foreignRequests)
		{
			if (exactRates.TryGetValue(key: request, out decimal rate))
				result[request] = new ConversionResult(Rate: rate, IsPending: false);
			else
				missingPairs.Add(item: new CurrencyLatestRateRequest(From: request.From, To: request.To));
		}

		if (missingPairs.Count == 0)
			return result;

		logger.ZLogWarning(message: $"No exact rates for {missingPairs.Count} pair(s) in batch, using latest available.");

		Dictionary<CurrencyLatestRateRequest, decimal> latestRates = await currencyRateReadRepository.GetLatestRatesBatchAsync(pairs: missingPairs, ct: ct);

		foreach (CurrencyRateRequest request in foreignRequests.Where(predicate: r => !result.ContainsKey(key: r)))
		{
			CurrencyLatestRateRequest latestKey = new CurrencyLatestRateRequest(From: request.From, To: request.To);

			if (latestRates.TryGetValue(key: latestKey, out decimal latestRate))
				result[request] = new ConversionResult(Rate: latestRate, IsPending: true);
			else
			{
				logger.ZLogError(message: $"No exchange rate found for {request.From} > {request.To}.");
				throw new CurrencyRateNotFoundException(
					message: $"The exchange rate for {request.From} > {request.To} was not found.",
					fromCurrency: request.From,
					toCurrency: request.To
				);
			}
		}

		return result;
	}
}