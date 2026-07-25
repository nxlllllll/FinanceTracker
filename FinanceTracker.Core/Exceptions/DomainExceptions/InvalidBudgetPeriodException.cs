namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "budget.invalid_period")]
public sealed class InvalidBudgetPeriodException(string message) : DomainException(message: message);
