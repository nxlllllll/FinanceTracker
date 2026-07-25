namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "budget.inactive")]
public sealed class InactiveBudgetException(string message) : DomainException(message: message);
