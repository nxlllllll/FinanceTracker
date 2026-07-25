namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "idempotency.abandoned")]
public sealed class IdempotencyAbandonedException(string message) : DomainException(message: message);
