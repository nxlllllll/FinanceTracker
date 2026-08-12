using System.Text.Json;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedCurrencyReadRepositoryTests
{
	private ICurrencyReadRepository _inner = null!;
	private IConnectionMultiplexer _connectionMultiplexer = null!;
	private IDatabase _database = null!;
	private CachedCurrencyReadRepository _repository = null!;

	private static readonly CurrencyInfo RubDto = new CurrencyInfo(
		Code: "RUB",
		Name: "Российский рубль",
		Symbol: "₽",
		IsActive: true
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<ICurrencyReadRepository>();
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
		_repository = new CachedCurrencyReadRepository(inner: _inner, redisCache: redisCache);

		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: RedisValue.Null);
	}

	[Test]
	public async Task GetAllAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [RubDto]);

		await _repository.GetAllAsync();

		await _inner.Received(requiredNumberOfCalls: 1).GetAllAsync(ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetAllAsync_OnCacheMiss_StoresResultInCache()
	{
		_inner.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [RubDto]);

		await _repository.GetAllAsync();

		await _database.Received(requiredNumberOfCalls: 1).StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		);
	}

	[Test]
	public async Task GetAllAsync_OnCacheHit_DoesNotCallInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: new List<CurrencyInfo> { RubDto }));

		await _repository.GetAllAsync();

		await _inner.DidNotReceive().GetAllAsync(ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetAllAsync_OnCacheHit_ReturnsCorrectValue()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: new List<CurrencyInfo> { RubDto }));

		IReadOnlyList<CurrencyInfo> result = await _repository.GetAllAsync();

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].Code).IsEqualTo(expected: "RUB");
	}

	[Test]
	public async Task GetByCodeAsync_OnCacheMiss_CallsInner()
	{
		_inner.GetByCodeAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: RubDto);

		await _repository.GetByCodeAsync(code: "RUB");

		await _inner.Received(requiredNumberOfCalls: 1).GetByCodeAsync(
			code: "RUB",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task GetByCodeAsync_OnCacheHit_DoesNotCallInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: RubDto));

		await _repository.GetByCodeAsync(code: "RUB");

		await _inner.DidNotReceive().GetByCodeAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetByCodeAsync_WhenInnerReturnsNull_CachesNull()
	{
		_inner.GetByCodeAsync(
			code: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (CurrencyInfo?)null);

		CurrencyInfo? result = await _repository.GetByCodeAsync(code: "XXX");

		await Assert.That(value: result).IsNull();
		await _database.Received(requiredNumberOfCalls: 1).StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		);
	}

	[Test]
	public async Task GetByCodeAsync_WhenNullIsCached_ReturnsNullWithoutCallingInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: (CurrencyInfo?)null));

		CurrencyInfo? result = await _repository.GetByCodeAsync(code: "XXX");

		await Assert.That(value: result).IsNull();
		await _inner.DidNotReceive().GetByCodeAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task ExistsAsync_OnCacheMiss_CallsInner()
	{
		_inner.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		await _repository.ExistsAsync(code: "RUB");

		await _inner.Received(requiredNumberOfCalls: 1).ExistsAsync(
			code: "RUB",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ExistsAsync_OnCacheHit_DoesNotCallInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: true));

		await _repository.ExistsAsync(code: "RUB");

		await _inner.DidNotReceive().ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task ExistsAsync_WhenFalseIsCached_ReturnsFalseWithoutCallingInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: false));

		bool result = await _repository.ExistsAsync(code: "XXX");

		await Assert.That(value: result).IsFalse();
		await _inner.DidNotReceive().ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task ExistsAsync_WhenRedisIsUnavailable_FallsThroughToInnerInsteadOfThrowing()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).ThrowsAsync(ex: new RedisConnectionException(
			failureType: ConnectionFailureType.SocketFailure,
			message: "Connection lost.",
			flags: CommandFlags.None
		));
		_inner.ExistsAsync(code: Arg.Any<string>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		bool result = await _repository.ExistsAsync(code: "RUB");

		await Assert.That(value: result).IsTrue();
		await _inner.Received(requiredNumberOfCalls: 1).ExistsAsync(code: "RUB", ct: Arg.Any<CancellationToken>());
	}
}
