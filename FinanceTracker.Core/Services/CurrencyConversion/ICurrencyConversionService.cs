namespace FinanceTracker.Core.Services.CurrencyConversion;

public interface ICurrencyConversionService
{
	Task<ConversionResult> GetConversionRateAsync(
		string fromCurrency,
		string toCurrency,
		DateOnly date,
		CancellationToken ct = default
	);
}