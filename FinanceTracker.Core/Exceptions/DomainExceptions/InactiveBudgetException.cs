namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InactiveBudgetException(string message) : DomainException(message: message);
