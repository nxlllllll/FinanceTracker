using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Services.CurrencyConversion;

public interface ICurrencyConversionService
{
	Task<ConversionResult> GetConversionRateAsync(
		Currency fromCurrency,
		Currency toCurrency,
		DateOnly date,
		CancellationToken ct = default
	);
}