namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidBudgetPeriodException(string message) : Exception(message: message);