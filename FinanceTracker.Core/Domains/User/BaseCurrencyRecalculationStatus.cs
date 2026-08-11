namespace FinanceTracker.Core.Domains.User;

/// <summary>
/// Lifecycle of a category-total rebuild triggered by a base currency change.
/// </summary>
public enum BaseCurrencyRecalculationStatus
{
	/// <summary>Requested and waiting for a worker. Totals do not match the current base currency.</summary>
	Pending,

	/// <summary>A worker holds the lease and is rebuilding. Totals still do not match.</summary>
	InProgress,

	/// <summary>Totals match the current base currency.</summary>
	Completed,

	/// <summary>Retried up to the limit and abandoned. Totals stay unavailable until someone looks.</summary>
	Failed
}
