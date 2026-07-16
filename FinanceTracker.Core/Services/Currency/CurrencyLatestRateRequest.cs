namespace FinanceTracker.Core.Services.Currency;

/// <summary>
/// Request for the most recent available exchange rate between two currencies,
/// regardless of date. Used by <c>BalanceAdjustmentJob</c> when a specific
/// date rate is not yet available.
/// </summary>
public readonly record struct CurrencyLatestRateRequest(
	ValueObjects.Currency From,
	ValueObjects.Currency To
);
