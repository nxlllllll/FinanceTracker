namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class DeactivatingException(string message) : DomainException(message: message);