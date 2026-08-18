namespace FinanceTracker.Api.Endpoints.Budgets.Contracts;

/// <summary>
/// Period bounds are dates, not instants: a budget covers calendar days, and both ends are
/// inclusive.
/// </summary>
public sealed record CreateBudgetRequest(
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DateOnly From,
	DateOnly To
);
