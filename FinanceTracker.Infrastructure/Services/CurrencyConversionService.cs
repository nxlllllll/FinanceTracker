using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Services;

public sealed class CurrencyConversionService(
	ICurrencyRateReadRepository currencyRateReadRepository,
	ILogger<CurrencyConversionService> logger
) : ICurrencyConversionService
{
	public async Task<ConversionResult> GetConversionRateAsync(
		Currency fromCurrency,
		Currency toCurrency,
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

		logger.ZLogInformation(message: $"The exact exchange rate as of {date:dd.MM.yyyy} for '{fromCurrency}' -> '{toCurrency}' is not set.");
		decimal? latestRate = await currencyRateReadRepository.GetLatestRateAsync(
			baseCurrencyCode: fromCurrency,
			targetCurrencyCode: toCurrency,
			ct: ct
		);

		if (latestRate is not null)
			return new ConversionResult(Rate: latestRate.Value, IsPending: true);

		logger.ZLogInformation(message: $"The exchange rate for '{fromCurrency}' -> '{toCurrency}' is not set.");
		throw new CurrencyRateNotFoundException(
			message: $"The exchange rate for {fromCurrency} → {toCurrency} was not found.",
			fromCurrency: fromCurrency,
			toCurrency: toCurrency
		);
	}
}