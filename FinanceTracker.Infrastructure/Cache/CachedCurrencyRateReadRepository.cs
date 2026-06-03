using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
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

	private static string RateKey(CurrencyRateRequest request)
		=> $"rate:{request.From.Value}:{request.To.Value}:{request.Date:yyyyMMdd}";

	private static string LatestRateKey(Core.ValueObjects.Currency from, Core.ValueObjects.Currency to)
		=> $"rate:latest:{from.Value}:{to.Value}";

	public async Task<decimal?> GetRateAsync(
		Core.ValueObjects.Currency baseCurrencyCode,
		Core.ValueObjects.Currency targetCurrencyCode,
		DateOnly date,
		CancellationToken ct = default)
	{
		string key = RateKey(request: new CurrencyRateRequest(From: baseCurrencyCode, To: targetCurrencyCode, Date: date));
		CacheEntry<decimal?> entry = await redisCache.TryGetAsync<decimal?>(key: key, ct: ct);
		if (entry.Found) 
			return entry.Value;

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
		string key = LatestRateKey(from: baseCurrencyCode, to: targetCurrencyCode);
		CacheEntry<decimal?> entry = await redisCache.TryGetAsync<decimal?>(key: key, ct: ct);
		if (entry.Found) 
			return entry.Value;

		decimal? result = await inner.GetLatestRateAsync(
			baseCurrencyCode: baseCurrencyCode,
			targetCurrencyCode: targetCurrencyCode,
			ct: ct
		);
		await redisCache.SetAsync(key: key, value: result, options: EndOfDay, ct: ct);
		return result;
	}

	public async Task<Dictionary<CurrencyRateRequest, decimal>> GetRatesBatchAsync(
		IReadOnlyCollection<CurrencyRateRequest> requests,
		CancellationToken ct = default)
	{
		if (requests.Count == 0)
			return [];

		Dictionary<CurrencyRateRequest, decimal> result = [];
		List<CurrencyRateRequest> cacheMisses = [];

		foreach (CurrencyRateRequest request in requests)
		{
			if (request.From == request.To)
			{
				result[request] = 1m;
				continue;
			}

			CacheEntry<decimal?> entry = await redisCache.TryGetAsync<decimal?>(key: RateKey(request: request), ct: ct);
			if (entry is { Found: true, Value: not null })
				result[request] = entry.Value.Value;
			else
				cacheMisses.Add(item: request);
		}

		if (cacheMisses.Count == 0)
			return result;

		Dictionary<CurrencyRateRequest, decimal> dbResults = await inner.GetRatesBatchAsync(requests: cacheMisses, ct: ct);

		foreach (CurrencyRateRequest request in cacheMisses)
		{
			string key = RateKey(request: request);
			if (dbResults.TryGetValue(key: request, out decimal rate))
			{
				result[request] = rate;
				await redisCache.SetAsync(key: key, value: (decimal?)rate, options: EndOfDay, ct: ct);
			}
			else await redisCache.SetAsync(key: key, value: (decimal?)null, options: EndOfDay, ct: ct);
		}

		return result;
	}
}