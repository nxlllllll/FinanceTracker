namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class OverlappingBudgetException(string message) : DomainException(message);
