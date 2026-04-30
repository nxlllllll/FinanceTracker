using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.CurrencyConversion;

namespace FinanceTracker.Infrastructure.Services;

public sealed class CurrencyConversionService(
	ICurrencyRateReadRepository currencyRateReadRepository
) : ICurrencyConversionService
{
	public async Task<ConversionResult> GetConversionRateAsync(
		string fromCurrency,
		string toCurrency,
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

		decimal? latestRate = await currencyRateReadRepository.GetLatestRateAsync(
			baseCurrencyCode: fromCurrency,
			targetCurrencyCode: toCurrency,
			ct: ct
		);

		if (latestRate is not null)
			return new ConversionResult(Rate: latestRate.Value, IsPending: true);

		throw new CurrencyRateNotFoundException(
			message: $"The exchange rate for {fromCurrency} → {toCurrency} was not found.",
			fromCurrency: fromCurrency,
			toCurrency: toCurrency
		);
	}
}