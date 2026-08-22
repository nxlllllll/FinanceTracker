using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Services.Currency;

public sealed class CurrencyConversionService(
	ICurrencyRateReadRepository currencyRateReadRepository,
	IDateProvider dateProvider,
	ILogger<CurrencyConversionService> logger
) : ICurrencyConversionService
{
	/// <summary>
	/// Resolves the rate to record against an operation, and how final that rate is.
	/// </summary>
	/// <exception cref="CurrencyRateMissingException">
	/// No rate — not even a "latest" one — has ever been recorded for this currency pair.
	/// </exception>
	public async Task<ConversionResult> GetConversionRateAsync(
		Core.ValueObjects.Currency fromCurrency,
		Core.ValueObjects.Currency toCurrency,
		DateOnly date,
		CancellationToken ct = default)
	{
		if (fromCurrency == toCurrency)
			return new ConversionResult(Rate: 1m, Status: RateStatus.Exact);

		decimal? exactRate = await currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: fromCurrency,
			targetCurrencyCode: toCurrency,
			date: date,
			ct: ct
		);

		if (exactRate is not null)
			return new ConversionResult(Rate: exactRate.Value, Status: RateStatus.Exact);

		decimal? latestRate = await currencyRateReadRepository.GetLatestRateAsync(
			baseCurrencyCode: fromCurrency,
			targetCurrencyCode: toCurrency,
			ct: ct
		);

		if (latestRate is null)
		{
			logger.ZLogError(message: $"No exchange rate found at all for {fromCurrency} > {toCurrency}.");
			throw new CurrencyRateMissingException(
				message: $"The exchange rate for {fromCurrency} > {toCurrency} was not found.",
				fromCurrency: fromCurrency,
				toCurrency: toCurrency
			);
		}

		bool rateCanStillArrive = date >= dateProvider.UtcToday;

		if (rateCanStillArrive)
		{
			logger.ZLogInformation(message: $"""
				No rate yet for {fromCurrency} > {toCurrency} on {date:dd.MM.yyyy}. Using latest as a placeholder; the adjustment job will correct it.
			""");
			return new ConversionResult(Rate: latestRate.Value, Status: RateStatus.Pending);
		}

		logger.ZLogWarning(message: $"""
			No rate for {fromCurrency} > {toCurrency} on {date:dd.MM.yyyy} and none will arrive — the date is in the past and rate history is never back-filled.
			Recording the latest known rate as an approximation.
		""");
		return new ConversionResult(Rate: latestRate.Value, Status: RateStatus.Approximated);
	}

	/// <exception cref="CurrencyRateMissingException">
	/// No rate was ever recorded at or before <paramref name="asOf"/>.
	/// </exception>
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
		throw new CurrencyRateMissingException(
			message: $"The exchange rate for {fromCurrency} > {toCurrency} was not found.",
			fromCurrency: fromCurrency,
			toCurrency: toCurrency
		);
	}

	/// <exception cref="CurrencyRateMissingException">
	/// A rate was missing for at least one request in the batch. Fails the whole batch rather than
	/// partially resolving it — a category total or budget progress computed from a partial rate set
	/// would be silently wrong for exactly the entries that hit the gap.
	/// </exception>
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
			throw new CurrencyRateMissingException(
				message: $"The exchange rate for {request.From} > {request.To} was not found.",
				fromCurrency: request.From,
				toCurrency: request.To
			);
		}

		return rates;
	}
}
