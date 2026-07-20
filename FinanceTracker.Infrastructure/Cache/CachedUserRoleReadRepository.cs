using FinanceTracker.Core.Repositories.Role;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Decorator for <see cref="IUserRoleReadRepository"/>, caching per role
/// checks in Redis. Short-TTL-plus-invalidation philosophy: role
/// assignment/removal invalidates the relevant key directly
/// </summary>
public sealed class CachedUserRoleReadRepository(
	IUserRoleReadRepository inner,
	RedisCache redisCache
) : IUserRoleReadRepository
{
	private static readonly DistributedCacheEntryOptions Ttl = new DistributedCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes: 3)
	};

	internal static string KeyFor(Guid userId, string systemKey) => $"roles:{userId}:{systemKey}";

	public async Task<bool> HasSystemRoleAsync(Guid userId, string systemKey, CancellationToken ct = default)
	{
		string key = KeyFor(userId: userId, systemKey: systemKey);

		CacheEntry<bool> entry = await redisCache.TryGetAsync<bool>(key: key);
		if (entry.Found)
			return entry.Value;

		bool result = await inner.HasSystemRoleAsync(userId: userId, systemKey: systemKey, ct: ct);
		await redisCache.SetAsync(key: key, value: result, options: Ttl);
		return result;
	}
}
