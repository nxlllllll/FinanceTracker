namespace FinanceTracker.Core.Repositories.Idempotency;

/// <summary>
/// Represents a stored idempotency record for an in-flight or completed command.
/// A <c>null</c> <see cref="ResponseJson"/> means the command is still being processed.
/// </summary>
/// <param name="ReservationId">
/// Identifies the specific reservation attempt that owns this row. Used to detect whether a
/// reservation has been reassigned (e.g. reclaimed as abandoned by another request) between
/// the time it was read and the time a caller tries to act on it.
/// </param>
/// <param name="ResponseJson">The serialized command response, or <c>null</c> if the command has not yet completed.</param>
/// <param name="ReservedAt">UTC timestamp when the idempotency key was first reserved.</param>
public sealed record IdempotencyEntry(
	Guid ReservationId,
	string? ResponseJson,
	DateTimeOffset ReservedAt
);
