namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Budget;

[ErrorCode(code: "budget.invalid_period")]
public sealed class InvalidBudgetPeriodException(string message) : DomainException(message: message);
