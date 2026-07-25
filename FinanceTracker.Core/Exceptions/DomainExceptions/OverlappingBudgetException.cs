namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "budget.overlapping_period")]
public sealed class OverlappingBudgetException(string message) : DomainException(message);
