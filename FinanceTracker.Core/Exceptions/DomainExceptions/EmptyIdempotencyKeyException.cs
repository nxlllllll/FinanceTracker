namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class EmptyIdempotencyKeyException(string message) : DomainException(message: message);
