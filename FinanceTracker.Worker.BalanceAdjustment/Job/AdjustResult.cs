namespace FinanceTracker.Worker.BalanceAdjustment.Job;

/// <summary>
/// Outcome of a single item processed by <c>BalanceAdjustmentJob</c>.
/// Used to aggregate metrics after each job run.
/// </summary>
public enum AdjustResult
{
	/// <summary>The exchange rate was found and the balance was recalculated successfully.</summary>
	Adjusted,

	/// <summary>No rate update was needed (rate unchanged or item already up to date).</summary>
	Skipped,

	/// <summary>Processing failed — logged and counted but not retried in the same run.</summary>
	Failed
}