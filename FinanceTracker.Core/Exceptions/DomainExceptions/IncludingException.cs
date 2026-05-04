namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class IncludingException(string message) : DomainException(message: message);