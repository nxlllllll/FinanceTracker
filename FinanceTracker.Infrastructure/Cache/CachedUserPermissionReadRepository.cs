using FinanceTracker.Core.Repositories.UserPermission;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Decorator for <see cref="IUserPermissionReadRepository"/> that caches a user's permission set in
/// Redis. Unlike currencies (24h TTL — rarely changes, safe to cache aggressively), authorization
/// data needs a short TTL: a revoked permission must stop working promptly. Grant/revoke also
/// invalidate this key directly (see <c>PermissionEventApplier</c>), so the TTL here is a safety
/// net for the invalidation-miss case (e.g. Redis was down at revoke time), not the primary
/// mechanism — hence short, not long.
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

	public static string KeyFor(Guid userId) => $"permissions:{userId}";

	public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default)
	{
		string key = KeyFor(userId: userId);

		CacheEntry<HashSet<string>> entry = await redisCache.TryGetAsync<HashSet<string>>(key: key);
		if (entry.Found)
			return entry.Value ?? [];

		IReadOnlySet<string> result = await inner.GetPermissionsAsync(userId: userId, ct: ct);
		await redisCache.SetAsync(key: key, value: (HashSet<string>)result, options: Ttl);
		return result;
	}
}
