namespace FinanceTracker.Core.Services.Currency;

public readonly record struct CurrencyRateRequest(
	ValueObjects.Currency From,
	ValueObjects.Currency To,
	DateOnly Date
);