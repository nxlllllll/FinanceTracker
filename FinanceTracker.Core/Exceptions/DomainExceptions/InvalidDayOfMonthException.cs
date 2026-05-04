namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidDayOfMonthException(string message) : DomainException(message: message);