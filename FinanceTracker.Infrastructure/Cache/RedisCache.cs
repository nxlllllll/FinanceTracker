using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class RedisCache(IDistributedCache cache)
{
	public async Task<CacheEntry<T>> TryGetAsync<T>(string key, CancellationToken ct = default)
	{
		byte[]? bytes = await cache.GetAsync(key: key, token: ct);
		if (bytes is null)
			return new CacheEntry<T>(Found: false, Value: default!);

		return new CacheEntry<T>(
			Found: true,
			Value: JsonSerializer.Deserialize<T>(utf8Json: bytes)!
		);
	}

	public async Task SetAsync<T>(
		string key,
		T value,
		DistributedCacheEntryOptions options,
		CancellationToken ct = default)
	{
		await cache.SetAsync(
			key: key,
			value: JsonSerializer.SerializeToUtf8Bytes(value: value),
			options: options,
			token: ct
		);
	}
}