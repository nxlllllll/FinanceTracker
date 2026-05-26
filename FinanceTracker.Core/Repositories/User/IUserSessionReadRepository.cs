namespace FinanceTracker.Core.Repositories.User;

public interface IUserSessionReadRepository
{
	Task<Domains.User.UserSession?> GetByRefreshTokenHashAsync(
		string tokenHash,
		CancellationToken ct = default
	);
}
