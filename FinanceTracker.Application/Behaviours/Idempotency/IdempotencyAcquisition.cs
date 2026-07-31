using FinanceTracker.Core.Exceptions.DomainExceptions;

namespace FinanceTracker.Application.Behaviours.Idempotency;

/// <summary>
/// The outcome of trying to acquire the right to execute an idempotent command:
/// either a cached response is already available, a fresh reservation was won, or
/// the attempt failed outright (abandoned, timed out).
/// </summary>
public record struct IdempotencyAcquisition(
	IdempotencyAcquisitionKind Kind,
	string? CachedResponseJson,
	Guid ReservationId,
	DomainException? Error)
{
	public static IdempotencyAcquisition CachedResponse(string json) => new IdempotencyAcquisition(
		Kind: IdempotencyAcquisitionKind.CachedResponse,
		CachedResponseJson: json,
		ReservationId: Guid.Empty,
		Error: null
	);

	public static IdempotencyAcquisition Reserved(Guid reservationId) => new IdempotencyAcquisition(
		Kind: IdempotencyAcquisitionKind.Reserved,
		CachedResponseJson: null,
		ReservationId: reservationId,
		Error: null
	);

	public static IdempotencyAcquisition Failed(DomainException error) => new IdempotencyAcquisition(
		Kind: IdempotencyAcquisitionKind.Failed,
		CachedResponseJson: null,
		ReservationId: Guid.Empty,
		Error: error
	);
}
