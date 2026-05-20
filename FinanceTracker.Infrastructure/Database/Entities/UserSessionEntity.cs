namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class UserSessionEntity
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public string RefreshTokenHash { get; set; } = String.Empty;
	public DateTime ExpiresAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? RevokedAt { get; set; }
}