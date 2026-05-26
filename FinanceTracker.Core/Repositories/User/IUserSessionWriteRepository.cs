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
}
