namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class ExcludingException(string message) : DomainException(message: message);