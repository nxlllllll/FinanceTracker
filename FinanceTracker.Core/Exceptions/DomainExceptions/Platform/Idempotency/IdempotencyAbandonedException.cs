namespace FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;

[ErrorCode(code: "idempotency.abandoned")]
public sealed class IdempotencyAbandonedException(string message) : DomainException(message: message);
