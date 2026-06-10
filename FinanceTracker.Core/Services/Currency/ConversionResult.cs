namespace FinanceTracker.Core.Services.Currency;

/// <summary>
/// Result of a currency conversion rate lookup.
/// When <see cref="IsPending"/> is <c>true</c>, no rate was found for the requested date
/// and a placeholder rate was used — the value will be corrected by <c>BalanceAdjustmentJob</c>.
/// <param name="Rate">The exchange rate: 1 unit of the source currency equals this many units of the target.</param>
/// <param name="IsPending">
/// <c>true</c> if no exact rate was available and a fallback was used.
/// Transactions with pending rates are reprocessed nightly.
/// </param>
/// </summary>
public sealed record ConversionResult(
	decimal Rate,
	bool IsPending
);