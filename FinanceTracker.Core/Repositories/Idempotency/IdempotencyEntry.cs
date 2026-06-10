namespace FinanceTracker.Core.Repositories.Idempotency;

/// <summary>
/// Represents a stored idempotency record for an in-flight or completed command.
/// A <c>null</c> <see cref="ResponseJson"/> means the command is still being processed.
/// </summary>
/// <param name="ResponseJson">The serialized command response, or <c>null</c> if the command has not yet completed.</param>
/// <param name="ReservedAt">UTC timestamp when the idempotency key was first reserved.</param>
public sealed record IdempotencyEntry(
	string? ResponseJson,
	DateTimeOffset ReservedAt
);