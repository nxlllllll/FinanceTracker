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

	public async Task<decimal> GetStableRateAsync(
		Core.ValueObjects.Currency fromCurrency,
		Core.ValueObjects.Currency toCurrency,
		DateTimeOffset asOf,
		CancellationToken ct = default)
	{
		decimal? rate = await currencyRateReadRepository.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: fromCurrency,
			targetCurrencyCode: toCurrency,
			asOf: asOf,
			ct: ct
		);

		if (rate is not null)
			return rate.Value;

		logger.ZLogError(message: $"No exchange rate known at or before {asOf:O} for {fromCurrency} > {toCurrency}.");
		throw new CurrencyRateNotFoundException(
			message: $"The exchange rate for {fromCurrency} > {toCurrency} was not found.",
			fromCurrency: fromCurrency,
			toCurrency: toCurrency
		);
	}

	public async Task<Dictionary<CurrencyStableRateRequest, decimal>> GetStableRatesBatchAsync(
		IReadOnlyCollection<CurrencyStableRateRequest> requests,
		CancellationToken ct = default)
	{
		if (requests.Count == 0)
			return [];

		Dictionary<CurrencyStableRateRequest, decimal> rates = await currencyRateReadRepository.GetRatesKnownAtOrBeforeBatchAsync(requests: requests, ct: ct);

		foreach (CurrencyStableRateRequest request in requests)
		{
			if (rates.ContainsKey(key: request))
				continue;

			logger.ZLogError(message: $"No exchange rate known at or before {request.AsOf:O} for {request.From} > {request.To}.");
			throw new CurrencyRateNotFoundException(
				message: $"The exchange rate for {request.From} > {request.To} was not found.",
				fromCurrency: request.From,
				toCurrency: request.To
			);
		}

		return rates;
	}
}
