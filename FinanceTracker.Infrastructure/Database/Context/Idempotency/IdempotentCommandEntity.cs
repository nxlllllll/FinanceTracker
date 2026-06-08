namespace FinanceTracker.Infrastructure.Database.Context.Idempotency;

public sealed class IdempotentCommandEntity
{
	public Guid IdempotencyKey { get; set; }
	public string CommandType { get; set; } = String.Empty;
	public string? ResponseJson { get; set; }
	public DateTimeOffset ReservedAt { get; set; }
	public DateTimeOffset ExpiresAt { get; set; }
}