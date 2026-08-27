using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedCurrencyRateWriteRepositoryTests
{
	private ICurrencyRateWriteRepository _inner = null!;
	private IConnectionMultiplexer _connectionMultiplexer = null!;
	private IDatabase _database = null!;
	private CachedCurrencyRateWriteRepository _repository = null!;

	private static readonly Currency Usd = Currency.Create(value: "USD").Value;
	private static readonly Currency Rub = Currency.Create(value: "RUB").Value;
	private static readonly Currency Eur = Currency.Create(value: "EUR").Value;
	private static readonly DateOnly Today = new DateOnly(year: 2025, month: 1, day: 15);
	private static readonly DateOnly Yesterday = Today.AddDays(value: -1);

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<ICurrencyRateWriteRepository>();
		_connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		_database = Substitute.For<IDatabase>();
		_connectionMultiplexer.GetDatabase(
			db: Arg.Any<int>(),
			asyncState: Arg.Any<object>()
		).Returns(returnThis: _database);
		_database.KeyDeleteAsync(keys: Arg.Any<RedisKey[]>()).Returns(returnThis: 0L);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions { InstanceName = "ft_test:" });

		RedisCache redisCache = new RedisCache(
			connectionMultiplexer: _connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);
		_repository = new CachedCurrencyRateWriteRepository(inner: _inner, redisCache: redisCache);
	}

	private static CurrencyRate Rate(Currency from, Currency to, decimal rate, DateOnly date)
		=> CurrencyRate.Reconstitute(baseCurrency: from, target: to, rate: rate, date: date);

	private Func<string[]> CaptureDeletedKeys()
	{
		RedisKey[]? captured = null;
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => captured = k)).Returns(returnThis: 0L);

		return () => captured is null ? [] : captured.Select(selector: k => (string)k!).ToArray();
	}

	[Test]
	public async Task UpsertRatesAsync_ShouldCallInnerWithTheSameRates()
	{
		IReadOnlyList<CurrencyRate> rates =
		[
			Rate(from: Usd, to: Rub, rate: 90m, date: Today)
		];

		await _repository.UpsertRatesAsync(rates: rates);

		await _inner.Received(requiredNumberOfCalls: 1).UpsertRatesAsync(rates: rates, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task UpsertRatesAsync_ShouldDeleteLatestRateKeyForEveryPair()
	{
		IReadOnlyList<CurrencyRate> rates =
		[
			Rate(from: Usd, to: Rub, rate: 90m, date: Today),
			Rate(from: Usd, to: Eur, rate: 0.9m, date: Today)
		];

		RedisKey[]? deletedKeys = null;
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deletedKeys = k)).Returns(returnThis: 2L);

		await _repository.UpsertRatesAsync(rates: rates);

		string[] keys = deletedKeys!.Select(selector: k => (string)k!).ToArray();

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: keys).Contains(expected: $"ft_test:rate:latest:{Usd.Value}:{Rub.Value}");
		await Assert.That(value: keys).Contains(expected: $"ft_test:rate:latest:{Usd.Value}:{Eur.Value}");
	}

	[Test]
	public async Task UpsertRatesAsync_ShouldDeleteTheDatedKeyForEveryRow()
	{
		IReadOnlyList<CurrencyRate> rates =
		[
			Rate(from: Usd, to: Rub, rate: 90m, date: Today),
			Rate(from: Usd, to: Eur, rate: 0.9m, date: Today)
		];

		Func<string[]> deletedKeys = CaptureDeletedKeys();

		await _repository.UpsertRatesAsync(rates: rates);

		string[] keys = deletedKeys();

		await Assert.That(value: keys).Contains(expected: $"ft_test:rate:{Usd.Value}:{Rub.Value}:20250115");
		await Assert.That(value: keys).Contains(expected: $"ft_test:rate:{Usd.Value}:{Eur.Value}:20250115").Because(message: """
			Rates are normally written once a day and read afterwards, so nothing collides. A corrective rerun
			for a day already cached is the case this covers: without the dated key every conversion of that
			day would keep using the superseded figure until midnight, with no error to notice.
		""");
	}

	[Test]
	public async Task UpsertRatesAsync_ShouldBuildTheDatedKeyFromTheRowsOwnDate()
	{
		IReadOnlyList<CurrencyRate> rates =
		[
			Rate(from: Usd, to: Rub, rate: 90m, date: Yesterday)
		];

		Func<string[]> deletedKeys = CaptureDeletedKeys();

		await _repository.UpsertRatesAsync(rates: rates);

		await Assert.That(value: deletedKeys()).Contains(expected: $"ft_test:rate:{Usd.Value}:{Rub.Value}:20250114").Because(message: """
			The date comes from the row, not from the clock. A back-fill writes yesterday's rate today, and
			invalidating today's key would clear an entry that is still correct while leaving the stale one.
		""");
	}

	[Test]
	public async Task UpsertRatesAsync_WithDuplicatePairsAcrossDates_ShouldDeleteTheLatestKeyOnlyOnce()
	{
		IReadOnlyList<CurrencyRate> rates =
		[
			Rate(from: Usd, to: Rub, rate: 91m, date: Today),
			Rate(from: Usd, to: Rub, rate: 90m, date: Yesterday)
		];

		RedisKey[]? deletedKeys = null;
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deletedKeys = k)).Returns(returnThis: 3L);

		await _repository.UpsertRatesAsync(rates: rates);

		string[] keys = deletedKeys!.Select(selector: k => (string)k!).ToArray();

		await Assert.That(value: keys.Count(predicate: k => k == $"ft_test:rate:latest:{Usd.Value}:{Rub.Value}")).IsEqualTo(expected: 1).Because(message: """
			One pair across two days invalidates one latest key and two dated ones. The pair repeats, so the
			latest key collapses; the dates differ, so the dated keys must not.
		""");
		await Assert.That(value: keys).Contains(expected: $"ft_test:rate:{Usd.Value}:{Rub.Value}:20250115");
		await Assert.That(value: keys).Contains(expected: $"ft_test:rate:{Usd.Value}:{Rub.Value}:20250114");
		await Assert.That(value: keys.Length).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task UpsertRatesAsync_ShouldLeaveTheStableKeysAlone()
	{
		IReadOnlyList<CurrencyRate> rates =
		[
			Rate(from: Usd, to: Rub, rate: 90m, date: Today)
		];

		Func<string[]> deletedKeys = CaptureDeletedKeys();

		await _repository.UpsertRatesAsync(rates: rates);

		string[] keys = deletedKeys();

		await Assert.That(value: keys).IsNotEmpty().Because(message: """
			Guards the assertion below: it checks for the absence of a prefix, and an empty capture would
			satisfy that without the call under test having done anything at all.
		""");

		await Assert.That(value: keys.Any(predicate: k => k.StartsWith(value: "ft_test:rate:stable:"))).IsFalse().Because(message: """
			This is a documented limitation, not an oversight, and the test exists so it is not mistaken for
			one. A stable key names the moment asked about down to the hour, so an upsert invalidates every
			one of them from its own date to now — a set that cannot be derived from the rows being written.
			They expire on their own after thirty days, which is also how long a back-filled historical rate
			stays invisible to GetRateKnownAtOrBeforeAsync.
		""");
	}

	[Test]
	public async Task UpsertRatesAsync_WithEmptyList_ShouldNotTouchRedis()
	{
		await _repository.UpsertRatesAsync(rates: []);

		await _inner.Received(requiredNumberOfCalls: 1).UpsertRatesAsync(
			rates: Arg.Any<IReadOnlyList<CurrencyRate>>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _database.DidNotReceive().KeyDeleteAsync(keys: Arg.Any<RedisKey[]>());
	}
}
