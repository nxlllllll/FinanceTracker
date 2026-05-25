namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class NameException(string message) : DomainException(message: message);
