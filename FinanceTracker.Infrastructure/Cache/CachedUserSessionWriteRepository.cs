using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedUserSessionWriteRepository(
	IUserSessionWriteRepository inner,
	RedisCache redisCache,
	IUnitOfWork unitOfWork
) : IUserSessionWriteRepository
{
	public Task CreateAsync(
		Core.Domains.User.UserSession session,
		CancellationToken ct = default
	) => inner.CreateAsync(session: session, ct: ct);

	public async Task<IReadOnlyList<Guid>> RevokeAsync(
		Guid sessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		IReadOnlyList<Guid> revokedIds = await inner.RevokeAsync(
			sessionId: sessionId,
			revokedAt: revokedAt,
			ct: ct
		);
		ScheduleCacheEviction(sessionIds: revokedIds);
		return revokedIds;
	}

	public async Task<IReadOnlyList<Guid>> SupersedeAsync(
		Guid sessionId,
		Guid successorSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		IReadOnlyList<Guid> revokedIds = await inner.SupersedeAsync(
			sessionId: sessionId,
			successorSessionId: successorSessionId,
			revokedAt: revokedAt,
			ct: ct
		);
		ScheduleCacheEviction(sessionIds: revokedIds);
		return revokedIds;
	}

	public async Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(
		Guid userId,
		Guid exceptSessionId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		IReadOnlyList<Guid> revokedIds = await inner.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: exceptSessionId,
			revokedAt: revokedAt,
			ct: ct
		);
		ScheduleCacheEviction(sessionIds: revokedIds);
		return revokedIds;
	}

	public async Task<IReadOnlyList<Guid>> RevokeAllAsync(
		Guid userId,
		DateTimeOffset revokedAt,
		CancellationToken ct = default)
	{
		IReadOnlyList<Guid> revokedIds = await inner.RevokeAllAsync(
			userId: userId,
			revokedAt: revokedAt,
			ct: ct
		);
		ScheduleCacheEviction(sessionIds: revokedIds);
		return revokedIds;
	}

	private void ScheduleCacheEviction(IReadOnlyList<Guid> sessionIds)
	{
		if (sessionIds.Count == 0)
			return;

		unitOfWork.OnCommitted(callback: () => EvictActiveMarksAsync(sessionIds: sessionIds));
	}

	private Task EvictActiveMarksAsync(IReadOnlyList<Guid> sessionIds)
	{
		List<string> keys = sessionIds.Select(selector: SessionCacheKeys.ActiveSessionKey).ToList();
		return redisCache.DeleteBatchAsync(keys: keys);
	}
}
