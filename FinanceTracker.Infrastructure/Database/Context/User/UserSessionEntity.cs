namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class UserSessionEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public string RefreshTokenHash { get; init; } = String.Empty;
	public DateTimeOffset ExpiresAt { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
	public DateTimeOffset? RevokedAt { get; init; }
}