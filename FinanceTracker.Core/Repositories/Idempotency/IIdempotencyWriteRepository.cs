namespace FinanceTracker.Core.Repositories.Idempotency;

public interface IIdempotencyWriteRepository
{
	/// <summary>
	/// Attempts to create a new reservation row. <paramref name="reservationId"/> uniquely
	/// identifies this specific attempt — later calls to <see cref="CompleteAsync"/> or
	/// <see cref="DeleteAsync"/> must present the same value to prove they still own the row.
	/// </summary>
	Task<bool> TryReserveAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		Guid reservationId,
		DateTimeOffset reservedAt,
		DateTimeOffset expiresAt,
		CancellationToken ct = default);

	/// <summary>
	/// Records the response for a reservation, but only if <paramref name="reservationId"/> still
	/// matches the row's current owner. Returns <c>false</c> if the reservation was reassigned
	/// (reclaimed as abandoned) since it was created — this as having lost
	/// the race and must not let its own side effects be committed.
	/// </summary>
	Task<bool> CompleteAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		Guid reservationId,
		string responseJson,
		CancellationToken ct = default);

	Task<int> DeleteExpiredAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default
	);

	/// <summary>
	/// Deletes a reservation row, but only if <paramref name="reservationId"/> still matches its
	/// current owner. Returns <c>false</c> if the row was already reassigned or removed by another
	/// request in the meantime.
	/// </summary>
	Task<bool> DeleteAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		Guid reservationId,
		CancellationToken ct = default);
}
