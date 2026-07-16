namespace FinanceTracker.Worker.BalanceAdjustment.Job;

/// <summary>
/// Outcome of a single item processed by <c>BalanceAdjustmentJob</c>.
/// </summary>
public enum AdjustResult
{
	/// <summary>The real rate arrived, the balance delta was posted, the rate lifecycle is closed.</summary>
	Resolved,

	/// <summary>The real rate never arrived within the grace period. The placeholder stands as final.</summary>
	Approximated,

	/// <summary>The rate arrived but the correction was rejected. Escalated to <c>unresolvable_events</c>.</summary>
	Unresolvable,

	/// <summary>The rate hasn't arrived yet and still might. Left queued, untouched.</summary>
	Waiting,

	/// <summary>Processing threw. The row stays pending and is retried on the next run.</summary>
	Failed
}
