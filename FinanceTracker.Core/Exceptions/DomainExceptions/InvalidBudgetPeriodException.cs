namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidBudgetPeriodException(string message) : DomainException(message: message);