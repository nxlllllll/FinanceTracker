namespace FinanceTracker.Infrastructure.Database.Context.User;

public sealed class UserSessionEntity
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public string RefreshTokenHash { get; set; } = String.Empty;
	public DateTimeOffset ExpiresAt { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset? RevokedAt { get; set; }
}
