namespace FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;

[ErrorCode(code: "idempotency.key_missing")]
public sealed class EmptyIdempotencyKeyException(string message) : DomainException(message: message);
