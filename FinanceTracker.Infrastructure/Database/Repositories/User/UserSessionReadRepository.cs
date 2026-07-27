using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserSessionReadRepository(
	FinanceTrackerContext context
) : IUserSessionReadRepository
{
	public async Task<Core.Domains.User.UserSession?> GetByRefreshTokenHashForUpdateAsync(
		string tokenHash,
		CancellationToken ct = default
	) => await context.GetSessionByRefreshTokenForUpdateAsync(tokenHash: tokenHash, ct: ct);

	public async Task<Core.Domains.User.UserSession?> GetByIdForUpdateAsync(
		Guid sessionId,
		CancellationToken ct = default
	) => await context.GetSessionByIdForUpdateAsync(sessionId: sessionId, ct: ct);
}
