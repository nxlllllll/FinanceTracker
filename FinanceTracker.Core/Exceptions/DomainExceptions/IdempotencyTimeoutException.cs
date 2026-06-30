namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class IdempotencyTimeoutException(string message) : DomainException(message: message);