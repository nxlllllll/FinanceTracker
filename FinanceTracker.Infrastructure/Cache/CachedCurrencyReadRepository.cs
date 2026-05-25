using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Currency;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

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

	public async Task<IReadOnlyList<CurrencyDto>> GetAllAsync(CancellationToken ct = default)
	{
		CacheEntry<IReadOnlyList<CurrencyDto>> entry = await redisCache.TryGetAsync<IReadOnlyList<CurrencyDto>>(key: AllKey, ct: ct);
		if (entry.Found)
			return entry.Value ?? [];

		IReadOnlyList<CurrencyDto> result = await inner.GetAllAsync(ct: ct);
		await redisCache.SetAsync(key: AllKey, value: result, options: Ttl, ct: ct);
		return result;
	}

	public async Task<IReadOnlyList<CurrencyDto>> GetAllActiveAsync(CancellationToken ct = default)
	{
		CacheEntry<IReadOnlyList<CurrencyDto>> entry = await redisCache.TryGetAsync<IReadOnlyList<CurrencyDto>>(key: AllActiveKey, ct: ct);
		if (entry.Found)
			return entry.Value ?? [];

		IReadOnlyList<CurrencyDto> result = await inner.GetAllActiveAsync(ct: ct);
		await redisCache.SetAsync(key: AllActiveKey, value: result, options: Ttl, ct: ct);
		return result;
	}

	public async Task<CurrencyDto?> GetByCodeAsync(string code, CancellationToken ct = default)
	{
		string key = $"currency:{code}";
		CacheEntry<CurrencyDto?> entry = await redisCache.TryGetAsync<CurrencyDto?>(key: key, ct: ct);
		if (entry.Found) 
			return entry.Value;

		CurrencyDto? result = await inner.GetByCodeAsync(code: code, ct: ct);
		await redisCache.SetAsync(key: key, value: result, options: Ttl, ct: ct);
		return result;
	}

	public async Task<bool> ExistsAsync(string code, CancellationToken ct = default)
	{
		string key = $"currency:exists:{code}";
		CacheEntry<bool> entry = await redisCache.TryGetAsync<bool>(key: key, ct: ct);
		if (entry.Found) 
			return entry.Value;

		bool result = await inner.ExistsAsync(code: code, ct: ct);
		await redisCache.SetAsync(key: key, value: result, options: Ttl, ct: ct);
		return result;
	}
}
