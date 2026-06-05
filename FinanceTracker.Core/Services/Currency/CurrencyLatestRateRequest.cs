namespace FinanceTracker.Core.Services.Currency;

public readonly record struct CurrencyLatestRateRequest(
	ValueObjects.Currency From,
	ValueObjects.Currency To
);