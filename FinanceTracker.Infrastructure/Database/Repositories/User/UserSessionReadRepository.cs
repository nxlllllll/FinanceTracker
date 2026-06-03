using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.User;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserSessionReadRepository(
	FinanceTrackerContext context
) : IUserSessionReadRepository
{
	public async Task<Core.Domains.User.UserSession?> GetByRefreshTokenHashForUpdateAsync(
		string tokenHash,
		CancellationToken ct = default)
	{
		return await context.UserSessions.FromSqlRaw(sql: """
			SELECT * FROM user_sessions
			WHERE refresh_token_hash = {0}
			LIMIT 1
			FOR UPDATE
		""", tokenHash).Select(selector: u => Core.Domains.User.UserSession.Reconstitute(
			id: u.Id,
			userId: u.UserId,
			refreshTokenHash: u.RefreshTokenHash,
			expiresAt: u.ExpiresAt,
			createdAt: u.CreatedAt,
			revokedAt: u.RevokedAt
		)).FirstOrDefaultAsync(cancellationToken: ct);
	}
}