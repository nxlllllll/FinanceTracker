namespace FinanceTracker.Core.Services.CurrencyConversion;

public sealed record ConversionResult(
	decimal Rate,
	bool IsPending
);