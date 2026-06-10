namespace FinanceTracker.Core.Services.Currency;

/// <summary>
/// Request for an exchange rate between two currencies on a specific date.
/// Used in <see cref="ICurrencyConversionService.GetConversionRatesBatchAsync"/> to load
/// multiple rates in a single query.
/// </summary>
public readonly record struct CurrencyRateRequest(
	ValueObjects.Currency From,
	ValueObjects.Currency To,
	DateOnly Date
);