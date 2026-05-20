namespace FinanceTracker.Core.Repositories.UserSession;

public interface IUserSessionWriteRepository
{
	Task CreateAsync(
		Domains.User.UserSession session,
		CancellationToken ct = default
	);

	Task RevokeAsync(
		Guid sessionId,
		DateTime revokedAt,
		CancellationToken ct = default
	);
}