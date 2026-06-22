namespace FinanceTracker.Core.Repositories.User;

public interface IUserSessionWriteRepository
{
	Task CreateAsync(
		Domains.User.UserSession session,
		CancellationToken ct = default
	);

	Task RevokeAsync(
		Guid sessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default
	);

	/// <summary>Revokes every currently-active session belonging to <paramref name="userId"/> except <paramref name="exceptSessionId"/>.</summary>
	Task RevokeAllExceptAsync(
		Guid userId,
		Guid exceptSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default
	);

	/// <summary>Revokes every currently-active session belonging to <paramref name="userId"/>.</summary>
	Task RevokeAllAsync(
		Guid userId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default
	);
}