namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidBudgetPeriodException(string message) : DomainException(message: message);