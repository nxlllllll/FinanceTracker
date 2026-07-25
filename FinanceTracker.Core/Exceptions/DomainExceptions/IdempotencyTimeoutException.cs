namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "idempotency.timeout")]
public sealed class IdempotencyTimeoutException(string message) : DomainException(message: message);
