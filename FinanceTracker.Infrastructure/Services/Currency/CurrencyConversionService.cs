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

		HashSet<(Core.ValueObjects.Currency From, Core.ValueObjects.Currency To)> missingPairs = [];

		foreach (CurrencyRateRequest request in foreignRequests)
		{
			if (exactRates.TryGetValue(key: request, out decimal rate))
				result[request] = new ConversionResult(Rate: rate, IsPending: false);
			else missingPairs.Add(item: (request.From, request.To));
		}

		if (missingPairs.Count == 0)
			return result;

		Dictionary<(Core.ValueObjects.Currency From, Core.ValueObjects.Currency To), decimal> latestRates = [];
		foreach ((Core.ValueObjects.Currency from, Core.ValueObjects.Currency to) in missingPairs)
		{
			logger.ZLogWarning(message: $"No exact rate for {from} > {to} in batch, using latest available.");
			decimal? latestRate = await currencyRateReadRepository.GetLatestRateAsync(
				baseCurrencyCode: from,
				targetCurrencyCode: to,
				ct: ct
			);

			if (latestRate is null)
			{
				logger.ZLogError(message: $"No exchange rate found for {from} > {to}.");
				throw new CurrencyRateNotFoundException(
					message: $"The exchange rate for {from} > {to} was not found.",
					fromCurrency: from,
					toCurrency: to
				);
			}

			latestRates[(from, to)] = latestRate.Value;
		}

		foreach (CurrencyRateRequest request in foreignRequests.Where(request => !result.ContainsKey(key: request)))
			result[request] = new ConversionResult(Rate: latestRates[(request.From, request.To)], IsPending: true);

		return result;
	}
}