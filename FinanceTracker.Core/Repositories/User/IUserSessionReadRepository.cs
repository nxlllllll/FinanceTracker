namespace FinanceTracker.Core.Repositories.User;

public interface IUserSessionReadRepository
{
	Task<Domains.User.UserSession?> GetByRefreshTokenHashForUpdateAsync(
		string tokenHash,
		CancellationToken ct = default
	);

	Task<Domains.User.UserSession?> GetByIdForUpdateAsync(
		Guid sessionId,
		CancellationToken ct = default
	);
}
