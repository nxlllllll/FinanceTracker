using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.User;
using FinanceTracker.Infrastructure.Database.Extensions;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class UserSessionWriteRepository(
	FinanceTrackerContext context
) : IUserSessionWriteRepository
{
	public async Task CreateAsync(
		UserSession session,
		CancellationToken ct = default)
	{
		await context.UserSessions.AddAsync(entity: new UserSessionEntity
		{
			Id = session.Id,
			UserId = session.UserId,
			RefreshTokenHash = session.RefreshTokenHash,
			ExpiresAt = session.ExpiresAt,
			CreatedAt = session.CreatedAt,
			RevokedAt = session.RevokedAt,
			SupersededBySessionId = session.SupersededBySessionId
		}, cancellationToken: ct);
	}

	public async Task<IReadOnlyList<Guid>> RevokeAsync(
		Guid sessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		return await context.RevokeUserSessionAsync(
			sessionId: sessionId,
			revokedAt: revokedAt,
			ct: ct
		);
	}

	public async Task<IReadOnlyList<Guid>> SupersedeAsync(
		Guid sessionId,
		Guid successorSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		return await context.SupersedeUserSessionAsync(
			sessionId: sessionId,
			successorSessionId: successorSessionId,
			revokedAt: revokedAt,
			ct: ct
		);
	}

	public async Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(
		Guid userId,
		Guid exceptSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		return await context.RevokeAllUserSessionsExceptAsync(
			userId: userId,
			exceptSessionId: exceptSessionId,
			revokedAt: revokedAt,
			ct: ct
		);
	}

	public async Task<IReadOnlyList<Guid>> RevokeAllAsync(
		Guid userId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		return await context.RevokeAllUserSessionsAsync(
			userId: userId,
			revokedAt: revokedAt,
			ct: ct
		);
	}
}
