using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Services.Token;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedUserSessionWriteRepository(
	IUserSessionWriteRepository inner,
	RedisCache redisCache,
	IUnitOfWork unitOfWork,
	IOptionsMonitor<JwtOptions> jwtOptions
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
		ScheduleCacheMarking(sessionIds: revokedIds);
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
		ScheduleCacheMarking(sessionIds: revokedIds);
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
		ScheduleCacheMarking(sessionIds: revokedIds);
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
		ScheduleCacheMarking(sessionIds: revokedIds);
		return revokedIds;
	}

	private void ScheduleCacheMarking(IReadOnlyList<Guid> sessionIds)
	{
		if (sessionIds.Count == 0)
			return;

		unitOfWork.OnCommitted(callback: () => MarkRevokedInCacheAsync(sessionIds: sessionIds));
	}

	private async Task MarkRevokedInCacheAsync(IReadOnlyList<Guid> sessionIds)
	{
		DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(value: jwtOptions.CurrentValue.AccessTokenTtlMinutes)
		};

		List<BatchItem<bool>> batch = sessionIds.Select(selector: id => new BatchItem<bool>(
			Key: SessionRevocationCacheKeys.RevokedSessionKey(sessionId: id),
			Value: true,
			Options: cacheOptions
		)).ToList();

		await redisCache.SetBatchAsync(items: batch);
	}
}
