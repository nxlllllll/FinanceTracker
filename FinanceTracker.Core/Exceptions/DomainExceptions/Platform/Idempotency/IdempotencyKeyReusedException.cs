namespace FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;

[ErrorCode(code: "idempotency.key_reused")]
public sealed class IdempotencyKeyReusedException(string message) : DomainException(message: message);
