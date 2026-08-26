using System.Text;
using System.Text.Json;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class RedisCachePoisonedValueTests
{
	private sealed record CachedShape(string Name, int Count);

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

	private static RedisValue Bytes(string content) => Encoding.UTF8.GetBytes(s: content);

	private static RedisValue Valid(CachedShape value) => JsonSerializer.SerializeToUtf8Bytes(value: value);

	private void GivenSingleValue(RedisValue value) => _database.StringGetAsync(
		key: Arg.Any<RedisKey>(),
		flags: Arg.Any<CommandFlags>()
	).Returns(returnThis: value);

	private void GivenBatchValues(params RedisValue[] values) => _database.StringGetAsync(
		keys: Arg.Any<RedisKey[]>(),
		flags: Arg.Any<CommandFlags>()
	).Returns(returnThis: values);

	[Test]
	public async Task TryGetAsync_WithAnUnreadableValue_ShouldReportAMiss()
	{
		GivenSingleValue(value: Bytes(content: "{ this is not json"));

		CacheEntry<CachedShape> entry = await _cache.TryGetAsync<CachedShape>(key: "shape:1");

		await Assert.That(value: entry.Found).IsFalse().Because(message: """
			Letting the parse failure through turned a stale cache entry into a 500 on every request that
			touched the key. A miss sends the caller to its source instead, which is what a cache is allowed
			to do at any moment anyway.
		""");
	}

	[Test]
	public async Task TryGetAsync_WithAnUnreadableValue_ShouldDropTheKey()
	{
		GivenSingleValue(value: Bytes(content: "{ this is not json"));

		await _cache.TryGetAsync<CachedShape>(key: "shape:1");

		await Task.Delay(millisecondsDelay: 50);

		await _database.Received().KeyDeleteAsync(
			keys: Arg.Is<RedisKey[]>(keys => keys.Any(k => k.ToString() == "ft_test:shape:1")),
			flags: Arg.Any<CommandFlags>()
		);
	}

	[Test]
	public async Task TryGetAsync_WithAValueOfTheWrongShape_ShouldReportAMiss()
	{
		GivenSingleValue(value: Bytes(content: """{"Name":"ok","Count":"not-a-number"}"""));

		CacheEntry<CachedShape> entry = await _cache.TryGetAsync<CachedShape>(key: "shape:1");

		await Assert.That(value: entry.Found).IsFalse().Because(message: """
			Well-formed JSON that no longer fits the type is the realistic version of this failure: the bytes
			were written by a previous build, not corrupted in transit.
		""");
	}

	[Test]
	public async Task TryGetAsync_WithAReadableValue_ShouldStillReturnIt()
	{
		GivenSingleValue(value: Valid(value: new CachedShape(Name: "ok", Count: 2)));

		CacheEntry<CachedShape> entry = await _cache.TryGetAsync<CachedShape>(key: "shape:1");

		await Assert.That(value: entry.Found).IsTrue();
		await Assert.That(value: entry.Value.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task TryGetAsync_WithAReadableValue_ShouldNotDropTheKey()
	{
		GivenSingleValue(value: Valid(value: new CachedShape(Name: "ok", Count: 2)));

		await _cache.TryGetAsync<CachedShape>(key: "shape:1");
		await Task.Delay(millisecondsDelay: 50);

		await _database.DidNotReceive().KeyDeleteAsync(
			keys: Arg.Any<RedisKey[]>(),
			flags: Arg.Any<CommandFlags>()
		);
	}

	[Test]
	public async Task TryGetBatchAsync_WithOnePoisonedValue_ShouldKeepTheOthers()
	{
		GivenBatchValues(
			Valid(value: new CachedShape(Name: "first", Count: 1)),
			Bytes(content: "{ broken"),
			Valid(value: new CachedShape(Name: "third", Count: 3))
		);

		Dictionary<string, CacheEntry<CachedShape>> result = await _cache.TryGetBatchAsync<CachedShape>(
			keys: ["shape:1", "shape:2", "shape:3"]
		);

		await Assert.That(value: result["shape:1"].Found).IsTrue();
		await Assert.That(value: result["shape:2"].Found).IsFalse();
		await Assert.That(value: result["shape:3"].Found).IsTrue().Because(message: """
			The parse used to happen while the result dictionary was being filled, so one bad entry threw
			before the rest were added and the caller lost every key in the batch — including the ones Redis
			had answered correctly.
		""");
	}

	[Test]
	public async Task TryGetBatchAsync_WithOnePoisonedValue_ShouldDropOnlyThatKey()
	{
		GivenBatchValues(
			Valid(value: new CachedShape(Name: "first", Count: 1)),
			Bytes(content: "{ broken")
		);

		await _cache.TryGetBatchAsync<CachedShape>(keys: ["shape:1", "shape:2"]);
		await Task.Delay(millisecondsDelay: 50);

		await _database.Received(requiredNumberOfCalls: 1).KeyDeleteAsync(
			keys: Arg.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0].ToString() == "ft_test:shape:2"),
			flags: Arg.Any<CommandFlags>()
		);
	}

	[Test]
	public async Task TryGetAsync_WhenTheKeyIsAbsent_ShouldReportAMissWithoutDeleting()
	{
		GivenSingleValue(value: RedisValue.Null);

		CacheEntry<CachedShape> entry = await _cache.TryGetAsync<CachedShape>(key: "shape:1");
		await Task.Delay(millisecondsDelay: 50);

		await Assert.That(value: entry.Found).IsFalse();

		await _database.DidNotReceive().KeyDeleteAsync(
			keys: Arg.Any<RedisKey[]>(),
			flags: Arg.Any<CommandFlags>()
		);
	}
}
