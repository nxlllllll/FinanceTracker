namespace FinanceTracker.Core.Domains.Abstractions.Rate;

/// <summary>
/// Lifecycle of the exchange rate attached to a financial operation (transaction or transfer).
/// </summary>
public enum RateStatus
{
	/// <summary>
	/// The exact rate for the operation's date was available at creation time.
	/// Nothing to correct — the recorded balance is already right.
	/// </summary>
	Exact,

	/// <summary>
	/// No exact rate existed for the operation's date, so the latest known rate was used as a
	/// placeholder. The operation is queued for <c>BalanceAdjustmentJob</c>, which replaces the
	/// placeholder with the real rate and posts the difference to the account balance.
	/// </summary>
	Pending,

	/// <summary>
	/// The exact rate arrived, the stored rate was replaced with it, and the resulting balance
	/// delta was applied. The operation is now recorded at the true rate.
	/// </summary>
	Resolved,

	/// <summary>
	/// The exact rate did not arrive within the grace period and is not expected to.
	/// The placeholder rate is accepted as final and the operation stops being retried.
	/// </summary>
	Approximated,

	/// <summary>
	/// The exact rate arrived, but the correction could not be applied. Escalated to
	/// <c>unresolvable_events</c> for manual resolution and never retried automatically.
	/// </summary>
	Unresolvable,

	/// <summary>
	/// The operation was undone before its rate was resolved — a transfer that was compensated or
	/// failed. Setting this is what stops the adjustment job from crediting a delta to an account
	/// that was never credited in the first place.
	/// </summary>
	Cancelled
}
