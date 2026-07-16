namespace FinanceTracker.Core.Services.Currency;

public readonly record struct CurrencyStableRateRequest(
	ValueObjects.Currency From,
	ValueObjects.Currency To,
	DateTimeOffset AsOf
);
