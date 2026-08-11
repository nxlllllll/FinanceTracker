namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Budget;

[ErrorCode(code: "budget.inactive")]
public sealed class InactiveBudgetException(string message) : DomainException(message: message);
