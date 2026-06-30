namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class IdempotencyAbandonedException(string message) : DomainException(message: message);