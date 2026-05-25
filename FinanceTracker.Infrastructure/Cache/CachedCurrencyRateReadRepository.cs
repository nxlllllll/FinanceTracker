using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedCurrencyRateReadRepository(
	ICurrencyRateReadRepository inner,
	RedisCache redisCache,
	IDateProvider dateProvider
) : ICurrencyRateReadRepository
{
	private DistributedCacheEntryOptions EndOfDay => new DistributedCacheEntryOptions
	{
		AbsoluteExpiration = dateProvider.UtcNow.AddDays(days: 1)
	};

	public async Task<decimal?> GetRateAsync(
		Core.ValueObjects.Currency baseCurrencyCode,
		Core.ValueObjects.Currency targetCurrencyCode,
		DateOnly date,
		CancellationToken ct = default)
	{
		string key = $"rate:{baseCurrencyCode.Value}:{targetCurrencyCode.Value}:{date:yyyyMMdd}";
		CacheEntry<decimal?> entry = await redisCache.TryGetAsync<decimal?>(key: key, ct: ct);
		if (entry.Found) return entry.Value;

		decimal? result = await inner.GetRateAsync(
			baseCurrencyCode: baseCurrencyCode,
			targetCurrencyCode: targetCurrencyCode,
			date: date,
			ct: ct
		);
		await redisCache.SetAsync(key: key, value: result, options: EndOfDay, ct: ct);
		return result;
	}

	public async Task<decimal?> GetLatestRateAsync(
		Core.ValueObjects.Currency baseCurrencyCode,
		Core.ValueObjects.Currency targetCurrencyCode,
		CancellationToken ct = default)
	{
		string key = $"rate:latest:{baseCurrencyCode.Value}:{targetCurrencyCode.Value}";
		CacheEntry<decimal?> entry = await redisCache.TryGetAsync<decimal?>(key: key, ct: ct);
		if (entry.Found) return entry.Value;

		decimal? result = await inner.GetLatestRateAsync(
			baseCurrencyCode: baseCurrencyCode,
			targetCurrencyCode: targetCurrencyCode,
			ct: ct
		);
		await redisCache.SetAsync(key: key, value: result, options: EndOfDay, ct: ct);
		return result;
	}
}
