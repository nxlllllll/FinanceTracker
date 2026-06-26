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

	/// <summary>
	/// Returns the rate already known (recorded) at or before <paramref name="asOf"/> — a stable,
	/// time-invariant answer for a fixed <paramref name="asOf"/>, unlike <see cref="GetConversionRateAsync"/>
	/// which can resolve differently over time when it falls back to "latest available".
	/// Throws <see cref="Exceptions.DomainExceptions.CurrencyRateNotFoundException"/> if no rate
	/// was ever recorded before <paramref name="asOf"/>.
	/// </summary>
	Task<decimal> GetStableRateAsync(
		ValueObjects.Currency fromCurrency,
		ValueObjects.Currency toCurrency,
		DateTimeOffset asOf,
		CancellationToken ct = default
	);

	/// <summary>Batch variant of <see cref="GetStableRateAsync"/>.</summary>
	Task<Dictionary<CurrencyStableRateRequest, decimal>> GetStableRatesBatchAsync(
		IReadOnlyCollection<CurrencyStableRateRequest> requests,
		CancellationToken ct = default
	);
}