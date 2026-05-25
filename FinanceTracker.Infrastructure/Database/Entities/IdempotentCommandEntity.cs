namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class IdempotentCommandEntity
{
	public Guid IdempotencyKey { get; init; }
	public string CommandType { get; init; } = String.Empty;
	public string ResponseJson { get; init; } = String.Empty;
	public DateTimeOffset CreatedAt { get; init; }
	public DateTimeOffset ExpiresAt { get; init; }
}
