namespace FinanceTracker.Core.Services.Currency;

public sealed record ConversionResult(
	decimal Rate,
	bool IsPending
);
