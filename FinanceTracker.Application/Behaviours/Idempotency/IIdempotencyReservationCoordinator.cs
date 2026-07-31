namespace FinanceTracker.Application.Behaviours.Idempotency;

/// <summary>
/// Owns the idempotency acquisition protocol: look up an existing reservation, return a cached
/// response, reserve a fresh slot, poll for an in-flight result, or reclaim an abandoned one.
/// </summary>
public interface IIdempotencyReservationCoordinator
{
	Task<IdempotencyAcquisition> AcquireAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		CancellationToken ct = default
	);
}
