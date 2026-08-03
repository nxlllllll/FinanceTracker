using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

/// <summary>
/// Covers what happens when Redis is unreachable. Every operation has to
/// degrade rather than throw, and has to say whether it actually landed.
/// </summary>
public sealed class RedisCacheTests
{
	private static readonly DistributedCacheEntryOptions Ttl = new DistributedCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes: 3)
	};

	private IDatabase _database = null!;
	private RedisCache _cache = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_database = Substitute.For<IDatabase>();

		IConnectionMultiplexer connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		connectionMultiplexer.GetDatabase(
			db: Arg.Any<int>(),
			asyncState: Arg.Any<object>()
		).Returns(returnThis: _database);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions { InstanceName = "ft_test:" });

		_cache = new RedisCache(
			connectionMultiplexer: connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);
	}

	private static RedisConnectionException Unavailable() => new RedisConnectionException(
		failureType: ConnectionFailureType.UnableToConnect,
		message: "unavailable"
	);

	[Test]
	public async Task SetAsync_WhenRedisAccepts_ShouldReportSuccess()
	{
		_database.StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		).Returns(returnThis: true);

		bool written = await _cache.SetAsync(key: "permissions:1", value: new[] { "account:read" }, options: Ttl);

		await Assert.That(value: written).IsTrue();
	}

	[Test]
	public async Task SetAsync_WhenRedisIsUnavailable_ShouldReportFailureWithoutThrowing()
	{
		_database.StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		).ThrowsAsync(ex: Unavailable());

		bool written = await _cache.SetAsync(key: "permissions:1", value: new[] { "account:read" }, options: Ttl);

		await Assert.That(value: written).IsFalse().Because(message: """
			The caller has to be able to tell this apart from success: the entry that was already there
			survives, so Redis keeps serving the value this write was meant to replace.
		""");
	}

	[Test]
	public async Task DeleteBatchAsync_WhenRedisAccepts_ShouldReportSuccess()
	{
		_database.KeyDeleteAsync(keys: Arg.Any<RedisKey[]>()).Returns(returnThis: 1L);

		bool deleted = await _cache.DeleteBatchAsync(keys: ["permissions:1"]);

		await Assert.That(value: deleted).IsTrue();
	}

	[Test]
	public async Task DeleteBatchAsync_WhenRedisIsUnavailable_ShouldReportFailureWithoutThrowing()
	{
		_database.KeyDeleteAsync(keys: Arg.Any<RedisKey[]>()).ThrowsAsync(ex: Unavailable());

		bool deleted = await _cache.DeleteBatchAsync(keys: ["permissions:1"]);

		await Assert.That(value: deleted).IsFalse();
	}

	[Test]
	public async Task DeleteBatchAsync_WithNoKeys_ShouldSucceedWithoutCallingRedis()
	{
		bool deleted = await _cache.DeleteBatchAsync(keys: []);

		await Assert.That(value: deleted).IsTrue();
		await _database.DidNotReceive().KeyDeleteAsync(keys: Arg.Any<RedisKey[]>());
	}

	[Test]
	public async Task TryGetAsync_WhenRedisIsUnavailable_ShouldReportAMiss()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).ThrowsAsync(ex: Unavailable());

		CacheEntry<string[]> entry = await _cache.TryGetAsync<string[]>(key: "permissions:1");

		await Assert.That(value: entry.Found).IsFalse().Because(message: """
			A read that cannot reach Redis has to behave like a miss, so the caller falls through to the
			database instead of failing the request.
		""");
	}

	[Test]
	public async Task TryGetBatchAsync_WhenRedisIsUnavailable_ShouldReportEveryKeyAsAMiss()
	{
		_database.StringGetAsync(keys: Arg.Any<RedisKey[]>()).ThrowsAsync(ex: Unavailable());

		Dictionary<string, CacheEntry<string[]>> result = await _cache.TryGetBatchAsync<string[]>(keys: ["a", "b"]);

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.Values.All(predicate: e => !e.Found)).IsTrue();
	}
}
