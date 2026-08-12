using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Repositories.Currency;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Decorator for <see cref="ICurrencyReadRepository"/> that caches the currency list in Redis
/// for 24 hours — the reference data changes rarely and is safe to cache aggressively.
/// </summary>
public sealed class CachedCurrencyReadRepository(
	ICurrencyReadRepository inner,
	RedisCache redisCache
) : ICurrencyReadRepository
{
	private static readonly DistributedCacheEntryOptions Ttl = new DistributedCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(hours: 24)
	};

	private const string AllKey = "currencies:all";
	private const string AllActiveKey = "currencies:active";

	public async Task<IReadOnlyList<CurrencyInfo>> GetAllAsync(CancellationToken ct = default)
	{
		CacheEntry<IReadOnlyList<CurrencyInfo>> entry = await redisCache.TryGetAsync<IReadOnlyList<CurrencyInfo>>(key: AllKey);
		if (entry.Found)
			return entry.Value ?? [];

		IReadOnlyList<CurrencyInfo> result = await inner.GetAllAsync(ct: ct);
		await redisCache.SetAsync(key: AllKey, value: result, options: Ttl);
		return result;
	}

	public async Task<IReadOnlyList<CurrencyInfo>> GetAllActiveAsync(CancellationToken ct = default)
	{
		CacheEntry<IReadOnlyList<CurrencyInfo>> entry = await redisCache.TryGetAsync<IReadOnlyList<CurrencyInfo>>(key: AllActiveKey);
		if (entry.Found)
			return entry.Value ?? [];

		IReadOnlyList<CurrencyInfo> result = await inner.GetAllActiveAsync(ct: ct);
		await redisCache.SetAsync(key: AllActiveKey, value: result, options: Ttl);
		return result;
	}

	public async Task<CurrencyInfo?> GetByCodeAsync(string code, CancellationToken ct = default)
	{
		string key = $"currency:{code}";
		CacheEntry<CurrencyInfo?> entry = await redisCache.TryGetAsync<CurrencyInfo?>(key: key);
		if (entry.Found)
			return entry.Value;

		CurrencyInfo? result = await inner.GetByCodeAsync(code: code, ct: ct);
		await redisCache.SetAsync(key: key, value: result, options: Ttl);
		return result;
	}

	public async Task<bool> ExistsAsync(string code, CancellationToken ct = default)
	{
		string key = $"currency:exists:{code}";
		CacheEntry<bool> entry = await redisCache.TryGetAsync<bool>(key: key);
		if (entry.Found)
			return entry.Value;

		bool result = await inner.ExistsAsync(code: code, ct: ct);
		await redisCache.SetAsync(key: key, value: result, options: Ttl);
		return result;
	}
}
