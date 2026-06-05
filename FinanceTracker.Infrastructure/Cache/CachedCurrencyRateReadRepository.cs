using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
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

	private static string LatestRateKey(Currency from, Currency to)
		=> $"rate:latest:{from.Value}:{to.Value}";

	public async Task<decimal?> GetRateAsync(
		Currency baseCurrencyCode,
		Currency targetCurrencyCode,
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
		Currency baseCurrencyCode,
		Currency targetCurrencyCode,
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
			else
				await redisCache.SetAsync(key: key, value: (decimal?)null, options: EndOfDay, ct: ct);
		}

		return result;
	}

	public async Task<Dictionary<CurrencyLatestRateRequest, decimal>> GetLatestRatesBatchAsync(
		IReadOnlyCollection<CurrencyLatestRateRequest> pairs,
		CancellationToken ct = default)
	{
		if (pairs.Count == 0)
			return [];

		Dictionary<CurrencyLatestRateRequest, decimal> result = [];
		List<CurrencyLatestRateRequest> cacheMisses = [];

		foreach (CurrencyLatestRateRequest pair in pairs)
		{
			if (pair.From == pair.To)
			{
				result[pair] = 1m;
				continue;
			}

			string key = LatestRateKey(from: pair.From, to: pair.To);
			CacheEntry<decimal?> entry = await redisCache.TryGetAsync<decimal?>(key: key, ct: ct);
			if (entry is { Found: true, Value: not null })
				result[pair] = entry.Value.Value;
			else
				cacheMisses.Add(item: pair);
		}

		if (cacheMisses.Count == 0)
			return result;

		Dictionary<CurrencyLatestRateRequest, decimal> dbResults = await inner.GetLatestRatesBatchAsync(pairs: cacheMisses, ct: ct);

		foreach (CurrencyLatestRateRequest pair in cacheMisses)
		{
			string key = LatestRateKey(from: pair.From, to: pair.To);
			if (dbResults.TryGetValue(key: pair, out decimal rate))
			{
				result[pair] = rate;
				await redisCache.SetAsync(key: key, value: (decimal?)rate, options: EndOfDay, ct: ct);
			}
			else
				await redisCache.SetAsync(key: key, value: (decimal?)null, options: EndOfDay, ct: ct);
		}

		return result;
	}
}