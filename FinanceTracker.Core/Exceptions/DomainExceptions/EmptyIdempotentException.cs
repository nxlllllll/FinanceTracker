namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class EmptyIdempotentException(string message) : DomainException(message: message);