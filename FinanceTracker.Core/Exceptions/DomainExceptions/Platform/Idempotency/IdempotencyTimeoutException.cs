namespace FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;

[ErrorCode(code: "idempotency.timeout")]
public sealed class IdempotencyTimeoutException(string message) : DomainException(message: message);
