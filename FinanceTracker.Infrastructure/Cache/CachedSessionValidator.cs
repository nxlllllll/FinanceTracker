using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Services.Token;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Resolves session state from Redis when possible and from the database otherwise.
/// </summary>
public sealed class CachedSessionValidator(
	IUserSessionReadRepository userSessionReadRepository,
	RedisCache redisCache,
	IDateProvider dateProvider,
	IOptionsMonitor<JwtOptions> jwtOptions
) : ISessionValidator
{
	public async Task<bool> IsSessionActiveAsync(
		Guid sessionId,
		CancellationToken ct = default)
	{
		string key = SessionCacheKeys.ActiveSessionKey(sessionId: sessionId);

		CacheEntry<bool> cached = await redisCache.TryGetAsync<bool>(key: key);
		if (cached.Found)
			return cached.Value;

		bool isActive = await userSessionReadRepository.IsActiveAsync(
			sessionId: sessionId,
			now: dateProvider.UtcNow,
			ct: ct
		);

		await redisCache.SetAsync(
			key: key,
			value: isActive,
			options: new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(value: jwtOptions.CurrentValue.ActiveSessionCacheSeconds)
			}
		);

		return isActive;
	}
}
