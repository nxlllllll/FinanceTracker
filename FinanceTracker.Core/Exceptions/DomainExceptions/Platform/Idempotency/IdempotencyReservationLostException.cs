namespace FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Idempotency;

[ErrorCode(code: "idempotency.reservation_lost")]
public sealed class IdempotencyReservationLostException(string message) : DomainException(message: message);
