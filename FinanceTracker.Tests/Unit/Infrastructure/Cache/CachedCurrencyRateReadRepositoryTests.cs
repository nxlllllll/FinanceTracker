using System.Text.Json;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedCurrencyRateReadRepositoryTests
{
	private ICurrencyRateReadRepository _inner = null!;
	private IDistributedCache _distributedCache = null!;
	private CachedCurrencyRateReadRepository _repository = null!;

	private static readonly Currency Usd = Currency.Create(value: "USD").Value;
	private static readonly Currency Rub = Currency.Create(value: "RUB").Value;
	private static readonly DateOnly Today = new DateOnly(year: 2025, month: 1, day: 15);
	private static readonly DateTimeOffset AsOf = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<ICurrencyRateReadRepository>();
		_distributedCache = Substitute.For<IDistributedCache>();

		RedisCache redisCache = new RedisCache(cache: _distributedCache);
		_repository = new CachedCurrencyRateReadRepository(inner: _inner, redisCache: redisCache, dateProvider: FakeDateProvider.Default);

		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: (byte[]?)null);
	}

	[Test]
	public async Task GetRateAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _repository.GetRateAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, date: Today);

		await _inner.Received(requiredNumberOfCalls: 1).GetRateAsync(
			baseCurrencyCode: Usd,
			targetCurrencyCode: Rub,
			date: Today,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateAsync_OnCacheMiss_StoresResultInCache()
	{
		_inner.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _repository.GetRateAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, date: Today);

		await _distributedCache.Received(requiredNumberOfCalls: 1).SetAsync(
			key: Arg.Any<string>(),
			value: Arg.Any<byte[]>(),
			options: Arg.Any<DistributedCacheEntryOptions>(),
			token: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateAsync_OnCacheHit_DoesNotCallInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(),
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)90m));

		await _repository.GetRateAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, date: Today);

		await _inner.DidNotReceive().GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateAsync_WhenInnerReturnsNull_CachesNull()
	{
		_inner.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (decimal?)null);

		decimal? result = await _repository.GetRateAsync(
			baseCurrencyCode: Usd,
			targetCurrencyCode: Rub,
			date: Today
		);

		await Assert.That(value: result).IsNull();
		await _distributedCache.Received(requiredNumberOfCalls: 1).SetAsync(
			key: Arg.Any<string>(),
			value: Arg.Any<byte[]>(),
			options: Arg.Any<DistributedCacheEntryOptions>(),
			token: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateAsync_WhenNullIsCached_ReturnsNullWithoutCallingInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)null));

		decimal? result = await _repository.GetRateAsync(
			baseCurrencyCode: Usd,
			targetCurrencyCode: Rub,
			date: Today
		);

		await Assert.That(value: result).IsNull();
		await _inner.DidNotReceive().GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetLatestRateAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetLatestRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _repository.GetLatestRateAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub);

		await _inner.Received(requiredNumberOfCalls: 1).GetLatestRateAsync(
			baseCurrencyCode: Usd,
			targetCurrencyCode: Rub,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetLatestRateAsync_OnCacheHit_DoesNotCallInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(), 
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)90m));

		await _repository.GetLatestRateAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub);

		await _inner.DidNotReceive().GetLatestRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _repository.GetRateKnownAtOrBeforeAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, asOf: AsOf);

		await _inner.Received(requiredNumberOfCalls: 1).GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Usd,
			targetCurrencyCode: Rub,
			asOf: AsOf,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_OnCacheHit_DoesNotCallInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(),
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)90m));

		await _repository.GetRateKnownAtOrBeforeAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, asOf: AsOf);

		await _inner.DidNotReceive().GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_WhenInnerReturnsNull_CachesNullWithStableTtl()
	{
		_inner.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (decimal?)null);

		decimal? result = await _repository.GetRateKnownAtOrBeforeAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, asOf: AsOf);

		await Assert.That(value: result).IsNull();

		await _distributedCache.Received(requiredNumberOfCalls: 1).SetAsync(
			key: Arg.Any<string>(),
			value: Arg.Any<byte[]>(),
			options: Arg.Is<DistributedCacheEntryOptions>(predicate: o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromDays(value: 30)),
			token: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_WhenNullIsCached_ReturnsNullWithoutCallingInner()
	{
		_distributedCache.GetAsync(
			key: Arg.Any<string>(),
			token: Arg.Any<CancellationToken>()
		).Returns(returnThis: JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)null));

		decimal? result = await _repository.GetRateKnownAtOrBeforeAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, asOf: AsOf);

		await Assert.That(value: result).IsNull();
		await _inner.DidNotReceive().GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}