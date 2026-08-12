namespace FinanceTracker.Core.ReadModels.Operation;

/// <summary>
/// Filter type for the unified operation history query.
/// Used to narrow results to a specific kind of financial activity.
/// </summary>
public enum OperationFilterType
{
	/// <summary>Credit transactions only (income).</summary>
	Income,

	/// <summary>Debit transactions only (expenses).</summary>
	Expense,

	/// <summary>Fund transfers between accounts.</summary>
	Transfer
}
