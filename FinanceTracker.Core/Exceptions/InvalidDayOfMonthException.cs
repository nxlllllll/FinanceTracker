namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidDayOfMonthException(string message) : Exception(message: message);