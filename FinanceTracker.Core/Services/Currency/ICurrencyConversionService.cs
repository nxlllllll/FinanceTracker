namespace FinanceTracker.Core.Services.Currency;

/// <summary>
/// Looks up exchange rates between currencies for a specific date.
/// Rates are sourced from the <c>currency_rates</c> table, populated by <c>CurrencyRateJob</c>.
/// </summary>
public interface ICurrencyConversionService
{
	/// <summary>
	/// Returns the conversion rate from <paramref name="fromCurrency"/> to
	/// <paramref name="toCurrency"/> on the given <paramref name="date"/>.
	/// Returns a failure result if no rate is found.
	/// </summary>
	Task<ConversionResult> GetConversionRateAsync(
		ValueObjects.Currency fromCurrency,
		ValueObjects.Currency toCurrency,
		DateOnly date,
		CancellationToken ct = default
	);

	/// <summary>
	/// Batch variant of <see cref="GetConversionRateAsync"/> for loading multiple
	/// rates in a single query. Used by <c>BalanceAdjustmentJob</c>.
	/// </summary>
	Task<Dictionary<CurrencyRateRequest, ConversionResult>> GetConversionRatesBatchAsync(
		IReadOnlyCollection<CurrencyRateRequest> requests,
		CancellationToken ct = default
	);
}