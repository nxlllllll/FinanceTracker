using FinanceTracker.Core.Repositories.UserSession;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserSession;

public class UserSessionReadRepository(
	FinanceTrackerContext context
) : IUserSessionReadRepository
{
	public async Task<Core.Domains.User.UserSession?> GetByRefreshTokenHashAsync(
		string tokenHash,
		CancellationToken ct = default)
	{
		UserSessionEntity? entity = await context.UserSessions.AsNoTracking()
			.Where(predicate: s => s.RefreshTokenHash == tokenHash)
			.FirstOrDefaultAsync(cancellationToken: ct);

		if (entity is null)
			return null;

		return Core.Domains.User.UserSession.Reconstitute(
			id: entity.Id,
			userId: entity.UserId,
			refreshTokenHash: entity.RefreshTokenHash,
			expiresAt: entity.ExpiresAt,
			createdAt: entity.CreatedAt,
			revokedAt: entity.RevokedAt
		);
	}
}
