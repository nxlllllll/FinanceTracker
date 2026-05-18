namespace FinanceTracker.Core.Services.Currency;

public interface ICurrencyConversionService
{
	Task<ConversionResult> GetConversionRateAsync(
		ValueObjects.Currency fromCurrency,
		ValueObjects.Currency toCurrency,
		DateOnly date,
		CancellationToken ct = default
	);
}