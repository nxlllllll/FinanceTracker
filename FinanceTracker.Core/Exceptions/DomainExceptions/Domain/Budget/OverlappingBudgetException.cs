namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Budget;

[ErrorCode(code: "budget.overlapping_period")]
public sealed class OverlappingBudgetException(
	string message,
	Guid? conflictingBudgetId = null
) : DomainException(message)
{
	public Guid? ConflictingBudgetId { get; } = conflictingBudgetId;
}
