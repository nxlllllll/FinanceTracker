using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Core.Repositories.User;

public interface IUserSessionReadRepository
{
	Task<UserSession?> GetByRefreshTokenHashForUpdateAsync(
		string tokenHash,
		CancellationToken ct = default
	);

	Task<UserSession?> GetByIdForUpdateAsync(
		Guid sessionId,
		CancellationToken ct = default
	);

	Task<bool> IsActiveAsync(
		Guid sessionId,
		DateTimeOffset now,
		CancellationToken ct = default
	);
}
