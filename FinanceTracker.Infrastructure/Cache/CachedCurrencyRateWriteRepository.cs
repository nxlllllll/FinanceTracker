using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Decorator for <see cref="ICurrencyRateWriteRepository"/> that invalidates the <c>rate:latest:*</c>
/// Redis entries for every currency pair in a batch immediately after it's upserted.
/// </summary>
public sealed class CachedCurrencyRateWriteRepository(
	ICurrencyRateWriteRepository inner,
	RedisCache redisCache
) : ICurrencyRateWriteRepository
{
	public async Task UpsertRatesAsync(
		IReadOnlyList<CurrencyRate> rates,
		CancellationToken ct = default)
	{
		await inner.UpsertRatesAsync(rates: rates, ct: ct);

		if (rates.Count == 0)
			return;

		List<string> keys = rates.Select(selector: r => CurrencyRateCacheKeys.LatestRateKey(from: r.Base, to: r.Target)).Distinct().ToList();
		await redisCache.DeleteBatchAsync(keys: keys);
	}
}
