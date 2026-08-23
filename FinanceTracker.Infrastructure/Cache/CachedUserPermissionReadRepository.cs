using FinanceTracker.Core.Repositories.UserPermission;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Decorator for <see cref="IUserPermissionReadRepository"/> that caches a user's permission set in Redis.
/// </summary>
public sealed class CachedUserPermissionReadRepository(
	IUserPermissionReadRepository inner,
	RedisCache redisCache
) : IUserPermissionReadRepository
{
	private static readonly DistributedCacheEntryOptions Ttl = new DistributedCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes: 3)
	};

	public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default)
	{
		string key = PermissionCacheKeys.Permissions(userId: userId);

		CacheEntry<HashSet<string>> entry = await redisCache.TryGetAsync<HashSet<string>>(key: key);
		if (entry.Found)
			return entry.Value ?? [];

		IReadOnlySet<string> result = await inner.GetPermissionsAsync(userId: userId, ct: ct);
		await redisCache.SetAsync(key: key, value: result.ToHashSet(), options: Ttl);

		return result;
	}
}
