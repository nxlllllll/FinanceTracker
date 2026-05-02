namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidDayOfMonthException(string message) : DomainException(message: message);