namespace FinanceTracker.Core.Services.Currency;

/// <summary>
/// Looks up exchange rates between currencies for a specific date.
/// Rates are sourced from the <c>currency_rates</c> table, populated by <c>CurrencyRateJob</c>.
/// </summary>
public interface ICurrencyConversionService
{
	/// <summary>
	/// Returns the conversion rate from <paramref name="fromCurrency"/> to
	/// <paramref name="toCurrency"/> on the given <paramref name="date"/>, together with how final
	/// that rate is — see <see cref="ConversionResult"/>.
	/// </summary>
	/// <exception cref="Exceptions.TransientExceptions.CurrencyRateMissingException">
	/// No rate — not even a "latest" one — has ever been recorded for this currency pair, usually
	/// because the rate job has not covered it yet. Thrown rather than returned via <c>Result</c>
	/// because workers rely on it: an unhandled throw is what makes a consumer reject the message
	/// and lets the queue retry it once the gap closes.
	/// </exception>
	Task<ConversionResult> GetConversionRateAsync(
		ValueObjects.Currency fromCurrency,
		ValueObjects.Currency toCurrency,
		DateOnly date,
		CancellationToken ct = default
	);

	/// <summary>
	/// Returns the rate already known (recorded) at or before <paramref name="asOf"/> — a stable,
	/// time-invariant answer for a fixed <paramref name="asOf"/>, unlike <see cref="GetConversionRateAsync"/>
	/// which can resolve differently over time when it falls back to "latest available".
	/// </summary>
	/// <exception cref="Exceptions.TransientExceptions.CurrencyRateMissingException">
	/// No rate was ever recorded at or before <paramref name="asOf"/>.
	/// </exception>
	Task<decimal> GetStableRateAsync(
		ValueObjects.Currency fromCurrency,
		ValueObjects.Currency toCurrency,
		DateTimeOffset asOf,
		CancellationToken ct = default
	);

	/// <summary>Batch variant of <see cref="GetStableRateAsync"/>.</summary>
	/// <exception cref="Exceptions.TransientExceptions.CurrencyRateMissingException">
	/// A rate was missing for at least one request in the batch.
	/// </exception>
	Task<Dictionary<CurrencyStableRateRequest, decimal>> GetStableRatesBatchAsync(
		IReadOnlyCollection<CurrencyStableRateRequest> requests,
		CancellationToken ct = default
	);
}
