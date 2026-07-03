using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Decorator for <see cref="ICurrencyRateReadRepository"/> that caches results in Redis
/// until end of day — rates change at most once per day via <c>CurrencyRateJob</c>.
/// </summary>
public sealed class CachedCurrencyRateReadRepository(
	ICurrencyRateReadRepository inner,
	RedisCache redisCache,
	IDateProvider dateProvider
) : ICurrencyRateReadRepository
{
	private static readonly TimeSpan NotFoundTtl = TimeSpan.FromMinutes(value: 1);
	private static readonly TimeSpan StableTtl = TimeSpan.FromDays(value: 30);

	private DistributedCacheEntryOptions EndOfDay
	{
		get
		{
			DateTimeOffset now = dateProvider.UtcNow;
			DateTimeOffset nextMidnightUtc = new DateTimeOffset(
				year: now.Year, month: now.Month, day: now.Day,
				hour: 0, minute: 0, second: 0,
				offset: TimeSpan.Zero
			).AddDays(days: 1);

			return new DistributedCacheEntryOptions { AbsoluteExpiration = nextMidnightUtc };
		}
	}

	private static DistributedCacheEntryOptions NotFound => new DistributedCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = NotFoundTtl
	};

	private static DistributedCacheEntryOptions Stable => new DistributedCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = StableTtl
	};

	private DistributedCacheEntryOptions OptionsFor(decimal? value)
		=> value is null ? NotFound : EndOfDay;

	private static string RateKey(CurrencyRateRequest request)
		=> $"rate:{request.From.Value}:{request.To.Value}:{request.Date:yyyyMMdd}";

	private static string LatestRateKey(Currency from, Currency to)
		=> $"rate:latest:{from.Value}:{to.Value}";

	private static string StableRateKey(CurrencyStableRateRequest request)
		=> $"rate:stable:{request.From.Value}:{request.To.Value}:{request.AsOf.UtcTicks}";

	/// <summary>
	/// Shared shape for every single-item method below: cache hit → return it; cache miss →
	/// ask <paramref name="fetch"/> (always a call into <see cref="inner"/>), cache whatever
	/// comes back under <paramref name="optionsFor"/>, then return it.
	/// </summary>
	private async Task<decimal?> GetOrFetchAsync(
		string key,
		Func<Task<decimal?>> fetch,
		Func<decimal?, DistributedCacheEntryOptions> optionsFor,
		CancellationToken ct)
	{
		CacheEntry<decimal?> entry = await redisCache.TryGetAsync<decimal?>(key: key, ct: ct);
		if (entry.Found)
			return entry.Value;

		decimal? result = await fetch();
		await redisCache.SetAsync(key: key, value: result, options: optionsFor(result), ct: ct);
		return result;
	}

	/// <summary>
	/// Shared first pass for every batch method below: resolves same-currency pairs immediately
	/// (no cache, no DB), and for the rest, checks the cache — splitting into already-cached
	/// hits (added directly to the result) and genuine cache misses (returned for the caller's
	/// own DB batch call).
	/// </summary>
	private async Task<(Dictionary<TRequest, decimal> Result, List<TRequest> CacheMisses)> SplitCacheAsync<TRequest>(
		IReadOnlyCollection<TRequest> requests,
		Func<TRequest, Currency> from,
		Func<TRequest, Currency> to,
		Func<TRequest, string> keyFor,
		CancellationToken ct)
		where TRequest : notnull
	{
		Dictionary<TRequest, decimal> result = [];
		List<TRequest> cacheMisses = [];

		foreach (TRequest request in requests)
		{
			if (from(request) == to(request))
			{
				result[request] = 1m;
				continue;
			}

			CacheEntry<decimal?> entry = await redisCache.TryGetAsync<decimal?>(key: keyFor(request), ct: ct);
			if (entry is { Found: true, Value: not null })
				result[request] = entry.Value.Value;
			else
				cacheMisses.Add(item: request);
		}

		return (result, cacheMisses);
	}

	/// <summary>
	/// Shared write-back for every batch method below: for each item that missed the cache,
	/// either record what the DB returned (and cache it under <paramref name="foundOptions"/>),
	/// or cache the absence under <paramref name="notFoundOptions"/>.
	/// </summary>
	private async Task WriteBackAsync<TRequest>(
		Dictionary<TRequest, decimal> result,
		List<TRequest> cacheMisses,
		Dictionary<TRequest, decimal> dbResults,
		Func<TRequest, string> keyFor,
		DistributedCacheEntryOptions foundOptions,
		DistributedCacheEntryOptions notFoundOptions,
		CancellationToken ct)
		where TRequest : notnull
	{
		foreach (TRequest request in cacheMisses)
		{
			string key = keyFor(request);
			if (dbResults.TryGetValue(key: request, out decimal rate))
			{
				result[request] = rate;
				await redisCache.SetAsync(key: key, value: (decimal?)rate, options: foundOptions, ct: ct);
			}
			else
				await redisCache.SetAsync(key: key, value: (decimal?)null, options: notFoundOptions, ct: ct);
		}
	}

	public Task<decimal?> GetRateAsync(
		Currency baseCurrencyCode,
		Currency targetCurrencyCode,
		DateOnly date,
		CancellationToken ct = default)
	{
		return GetOrFetchAsync(
			key: RateKey(request: new CurrencyRateRequest(From: baseCurrencyCode, To: targetCurrencyCode, Date: date)),
			fetch: () => inner.GetRateAsync(baseCurrencyCode: baseCurrencyCode, targetCurrencyCode: targetCurrencyCode, date: date, ct: ct),
			optionsFor: OptionsFor,
			ct: ct
		);
	}

	public Task<decimal?> GetLatestRateAsync(
		Currency baseCurrencyCode,
		Currency targetCurrencyCode,
		CancellationToken ct = default)
	{
		return GetOrFetchAsync(
			key: LatestRateKey(from: baseCurrencyCode, to: targetCurrencyCode),
			fetch: () => inner.GetLatestRateAsync(baseCurrencyCode: baseCurrencyCode, targetCurrencyCode: targetCurrencyCode, ct: ct),
			optionsFor: OptionsFor,
			ct: ct
		);
	}

	public Task<decimal?> GetRateKnownAtOrBeforeAsync(
		Currency baseCurrencyCode,
		Currency targetCurrencyCode,
		DateTimeOffset asOf,
		CancellationToken ct = default)
	{
		return GetOrFetchAsync(
			key: StableRateKey(request: new CurrencyStableRateRequest(From: baseCurrencyCode, To: targetCurrencyCode, AsOf: asOf)),
			fetch: () => inner.GetRateKnownAtOrBeforeAsync(baseCurrencyCode: baseCurrencyCode, targetCurrencyCode: targetCurrencyCode, asOf: asOf, ct: ct),
			optionsFor: _ => Stable,
			ct: ct
		);
	}

	public async Task<Dictionary<CurrencyLatestRateRequest, decimal>> GetLatestRatesBatchAsync(
		IReadOnlyCollection<CurrencyLatestRateRequest> pairs,
		CancellationToken ct = default)
	{
		if (pairs.Count == 0)
			return [];

		(Dictionary<CurrencyLatestRateRequest, decimal> result, List<CurrencyLatestRateRequest> cacheMisses) = await SplitCacheAsync(
			requests: pairs,
			from: p => p.From,
			to: p => p.To,
			keyFor: p => LatestRateKey(from: p.From, to: p.To),
			ct: ct
		);

		if (cacheMisses.Count == 0)
			return result;

		Dictionary<CurrencyLatestRateRequest, decimal> dbResults = await inner.GetLatestRatesBatchAsync(pairs: cacheMisses, ct: ct);

		await WriteBackAsync(
			result: result,
			cacheMisses: cacheMisses,
			dbResults: dbResults,
			keyFor: p => LatestRateKey(from: p.From, to: p.To),
			foundOptions: EndOfDay,
			notFoundOptions: NotFound,
			ct: ct
		);

		return result;
	}

	public async Task<Dictionary<CurrencyStableRateRequest, decimal>> GetRatesKnownAtOrBeforeBatchAsync(
		IReadOnlyCollection<CurrencyStableRateRequest> requests,
		CancellationToken ct = default)
	{
		if (requests.Count == 0)
			return [];

		(Dictionary<CurrencyStableRateRequest, decimal> result, List<CurrencyStableRateRequest> cacheMisses) = await SplitCacheAsync(
			requests: requests,
			from: r => r.From,
			to: r => r.To,
			keyFor: StableRateKey,
			ct: ct
		);

		if (cacheMisses.Count == 0)
			return result;

		Dictionary<CurrencyStableRateRequest, decimal> dbResults = await inner.GetRatesKnownAtOrBeforeBatchAsync(requests: cacheMisses, ct: ct);

		await WriteBackAsync(
			result: result,
			cacheMisses: cacheMisses,
			dbResults: dbResults,
			keyFor: StableRateKey,
			foundOptions: Stable,
			notFoundOptions: Stable,
			ct: ct
		);

		return result;
	}
}
