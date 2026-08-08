using System.Text.Json;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedCurrencyRateReadRepositoryTests
{
	private ICurrencyRateReadRepository _inner = null!;
	private IConnectionMultiplexer _connectionMultiplexer = null!;
	private IDatabase _database = null!;
	private CachedCurrencyRateReadRepository _repository = null!;

	private static readonly Currency Usd = Currency.Create(value: "USD").Value;
	private static readonly Currency Rub = Currency.Create(value: "RUB").Value;
	private static readonly DateOnly Today = new DateOnly(year: 2025, month: 1, day: 15);
	private static readonly DateTimeOffset AsOf = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<ICurrencyRateReadRepository>();
		_connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		_database = Substitute.For<IDatabase>();
		_connectionMultiplexer.GetDatabase(db: Arg.Any<int>(), asyncState: Arg.Any<object>()).Returns(returnThis: _database);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions { InstanceName = "ft_test:" });

		RedisCache redisCache = new RedisCache(
			connectionMultiplexer: _connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);
		_repository = new CachedCurrencyRateReadRepository(inner: _inner, redisCache: redisCache, dateProvider: FakeDateProvider.Default);

		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: RedisValue.Null);
	}

	private void SetupBatchCacheValues(Dictionary<string, decimal?> valuesByUnprefixedKey)
	{
		_database.StringGetAsync(keys: Arg.Any<RedisKey[]>()).Returns(returnThis: callInfo =>
		{
			RedisKey[] keys = callInfo.Arg<RedisKey[]>() ?? [];
			RedisValue[] values = new RedisValue[keys.Length];

			for (int i = 0; i < keys.Length; i++)
			{
				string unprefixed = ((string)keys[i]!).Replace(oldValue: "ft_test:", newValue: "");
				values[i] = valuesByUnprefixedKey.TryGetValue(key: unprefixed, out decimal? value) && value is not null
					? (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: value)
					: RedisValue.Null;
			}

			return values;
		});
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

		await _database.Received(requiredNumberOfCalls: 1).StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		);
	}

	[Test]
	public async Task GetRateAsync_OnCacheHit_DoesNotCallInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)90m));

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
		await _database.Received(requiredNumberOfCalls: 1).StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		);
	}

	[Test]
	public async Task GetRateAsync_WhenNullIsCached_ReturnsNullWithoutCallingInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)null));

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
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)90m));

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
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)90m));

		await _repository.GetRateKnownAtOrBeforeAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, asOf: AsOf);

		await _inner.DidNotReceive().GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_WhenInnerReturnsNull_CachesNull()
	{
		_inner.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (decimal?)null);

		decimal? result = await _repository.GetRateKnownAtOrBeforeAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, asOf: AsOf);

		await Assert.That(value: result).IsNull();

		await _database.Received(requiredNumberOfCalls: 1).StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		);
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_WhenNullIsCached_ReturnsNullWithoutCallingInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: (decimal?)null));

		decimal? result = await _repository.GetRateKnownAtOrBeforeAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, asOf: AsOf);

		await Assert.That(value: result).IsNull();
		await _inner.DidNotReceive().GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetRateAsync_WhenRedisIsUnavailable_FallsThroughToInnerInsteadOfThrowing()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).ThrowsAsync(ex: new RedisConnectionException(
			failureType: ConnectionFailureType.SocketFailure,
			message: "Connection lost.",
			flags: CommandFlags.None
		));
		_inner.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		decimal? result = await _repository.GetRateAsync(baseCurrencyCode: Usd, targetCurrencyCode: Rub, date: Today);

		await Assert.That(value: result).IsEqualTo(expected: 90m);
		await _inner.Received(requiredNumberOfCalls: 1).GetRateAsync(
			baseCurrencyCode: Usd,
			targetCurrencyCode: Rub,
			date: Today,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetLatestRatesBatchAsync_WithAllCacheMisses_ShouldUseSingleMgetCallInsteadOfPerKeyCalls()
	{
		CurrencyLatestRateRequest pair1 = new CurrencyLatestRateRequest(From: Usd, To: Rub);
		CurrencyLatestRateRequest pair2 = new CurrencyLatestRateRequest(From: Rub, To: Usd);

		SetupBatchCacheValues(valuesByUnprefixedKey: []);
		_inner.GetLatestRatesBatchAsync(
			pairs: Arg.Any<IReadOnlyCollection<CurrencyLatestRateRequest>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new Dictionary<CurrencyLatestRateRequest, decimal>
		{
			[pair1] = 90m,
			[pair2] = 0.0111m
		});

		await _repository.GetLatestRatesBatchAsync(pairs: [pair1, pair2]);

		await _database.Received(requiredNumberOfCalls: 1).StringGetAsync(keys: Arg.Any<RedisKey[]>());
		await _database.DidNotReceive().StringGetAsync(key: Arg.Any<RedisKey>());
	}

	[Test]
	public async Task GetLatestRatesBatchAsync_WithAllValuesCached_ShouldReturnCachedValuesInSingleCallWithoutCallingInner()
	{
		CurrencyLatestRateRequest pair1 = new CurrencyLatestRateRequest(From: Usd, To: Rub);
		CurrencyLatestRateRequest pair2 = new CurrencyLatestRateRequest(From: Rub, To: Usd);

		SetupBatchCacheValues(valuesByUnprefixedKey: new Dictionary<string, decimal?>
		{
			[CurrencyRateCacheKeys.LatestRateKey(from: Usd, to: Rub)] = 90m,
			[CurrencyRateCacheKeys.LatestRateKey(from: Rub, to: Usd)] = 0.0111m
		});

		Dictionary<CurrencyLatestRateRequest, decimal> result = await _repository.GetLatestRatesBatchAsync(pairs: [pair1, pair2]);

		await Assert.That(value: result[pair1]).IsEqualTo(expected: 90m);
		await Assert.That(value: result[pair2]).IsEqualTo(expected: 0.0111m);
		await _database.Received(requiredNumberOfCalls: 1).StringGetAsync(keys: Arg.Any<RedisKey[]>());
		await _database.DidNotReceive().StringGetAsync(key: Arg.Any<RedisKey>());
		await _inner.DidNotReceive().GetLatestRatesBatchAsync(
			pairs: Arg.Any<IReadOnlyCollection<CurrencyLatestRateRequest>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetLatestRatesBatchAsync_WithSameCurrencyPair_ShouldReturnOneWithoutTouchingCache()
	{
		CurrencyLatestRateRequest samePair = new CurrencyLatestRateRequest(From: Usd, To: Usd);

		Dictionary<CurrencyLatestRateRequest, decimal> result = await _repository.GetLatestRatesBatchAsync(pairs: [samePair]);

		await Assert.That(value: result[samePair]).IsEqualTo(expected: 1m);
		await _database.DidNotReceive().StringGetAsync(keys: Arg.Any<RedisKey[]>());
	}

	[Test]
	public async Task GetLatestRatesBatchAsync_OnCacheMiss_ShouldWriteBackInSingleBatchInsteadOfPerKeyCalls()
	{
		CurrencyLatestRateRequest pair1 = new CurrencyLatestRateRequest(From: Usd, To: Rub);
		CurrencyLatestRateRequest pair2 = new CurrencyLatestRateRequest(From: Rub, To: Usd);

		SetupBatchCacheValues(valuesByUnprefixedKey: []);
		_inner.GetLatestRatesBatchAsync(pairs: Arg.Any<IReadOnlyCollection<CurrencyLatestRateRequest>>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: new Dictionary<CurrencyLatestRateRequest, decimal> { [pair1] = 90m, [pair2] = 0.0111m });

		IBatch batch = Substitute.For<IBatch>();
		_database.CreateBatch(asyncState: Arg.Any<object>()).Returns(returnThis: batch);

		await _repository.GetLatestRatesBatchAsync(pairs: [pair1, pair2]);

		_database.Received(requiredNumberOfCalls: 1).CreateBatch(asyncState: Arg.Any<object>());
		batch.Received(requiredNumberOfCalls: 1).Execute();
	}

	[Test]
	public async Task GetRatesKnownAtOrBeforeBatchAsync_WithAllCacheMisses_ShouldUseSingleMgetCallInsteadOfPerKeyCalls()
	{
		CurrencyStableRateRequest request1 = new CurrencyStableRateRequest(From: Usd, To: Rub, AsOf: AsOf);
		CurrencyStableRateRequest request2 = new CurrencyStableRateRequest(From: Rub, To: Usd, AsOf: AsOf);

		SetupBatchCacheValues(valuesByUnprefixedKey: []);
		_inner.GetRatesKnownAtOrBeforeBatchAsync(requests: Arg.Any<IReadOnlyCollection<CurrencyStableRateRequest>>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: new Dictionary<CurrencyStableRateRequest, decimal> { [request1] = 90m, [request2] = 0.0111m });

		await _repository.GetRatesKnownAtOrBeforeBatchAsync(requests: [request1, request2]);

		await _database.Received(requiredNumberOfCalls: 1).StringGetAsync(keys: Arg.Any<RedisKey[]>());
		await _database.DidNotReceive().StringGetAsync(key: Arg.Any<RedisKey>());
	}

	[Test]
	public async Task GetRatesKnownAtOrBeforeBatchAsync_WithAllValuesCached_ShouldReturnCachedValuesInSingleCall()
	{
		CurrencyStableRateRequest request1 = new CurrencyStableRateRequest(From: Usd, To: Rub, AsOf: AsOf);
		CurrencyStableRateRequest request2 = new CurrencyStableRateRequest(From: Rub, To: Usd, AsOf: AsOf);

		SetupBatchCacheValues(valuesByUnprefixedKey: new Dictionary<string, decimal?>
		{
			[CurrencyRateCacheKeys.StableRateKey(request: request1)] = 90m,
			[CurrencyRateCacheKeys.StableRateKey(request: request2)] = 0.0111m
		});

		Dictionary<CurrencyStableRateRequest, decimal> result = await _repository.GetRatesKnownAtOrBeforeBatchAsync(requests: [request1, request2]);

		await Assert.That(value: result[request1]).IsEqualTo(expected: 90m);
		await Assert.That(value: result[request2]).IsEqualTo(expected: 0.0111m);
		await _database.Received(requiredNumberOfCalls: 1).StringGetAsync(keys: Arg.Any<RedisKey[]>());
		await _inner.DidNotReceive().GetRatesKnownAtOrBeforeBatchAsync(
			requests: Arg.Any<IReadOnlyCollection<CurrencyStableRateRequest>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
