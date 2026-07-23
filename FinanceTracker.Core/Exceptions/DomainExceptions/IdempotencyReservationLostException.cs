namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class IdempotencyReservationLostException(string message) : DomainException(message: message);
