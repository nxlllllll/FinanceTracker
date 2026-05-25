namespace FinanceTracker.Core.Repositories.UserSession;

public interface IUserSessionReadRepository
{
	Task<Domains.User.UserSession?> GetByRefreshTokenHashAsync(
		string tokenHash,
		CancellationToken ct = default
	);
}
