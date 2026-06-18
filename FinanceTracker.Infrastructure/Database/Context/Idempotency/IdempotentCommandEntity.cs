namespace FinanceTracker.Infrastructure.Database.Context.Idempotency;

public sealed class IdempotentCommandEntity
{
	public Guid IdempotencyKey { get; init; }
	public string CommandType { get; init; } = String.Empty;
	public string? ResponseJson { get; init; }
	public DateTimeOffset ReservedAt { get; init; }
	public DateTimeOffset ExpiresAt { get; init; }
}