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

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: deletedKeys!.Length).IsEqualTo(expected: 2);
		await Assert.That(value: deletedKeys!.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:rate:latest:{Usd.Value}:{Rub.Value}");
		await Assert.That(value: deletedKeys!.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:rate:latest:{Usd.Value}:{Eur.Value}");
	}

	[Test]
	public async Task UpsertRatesAsync_WithDuplicatePairsAcrossDates_ShouldDeleteEachKeyOnlyOnce()
	{
		IReadOnlyList<CurrencyRate> rates =
		[
			Rate(from: Usd, to: Rub, rate: 91m, date: Today),
			Rate(from: Usd, to: Rub, rate: 90m, date: Yesterday)
		];

		RedisKey[]? deletedKeys = null;
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deletedKeys = k)).Returns(returnThis: 1L);

		await _repository.UpsertRatesAsync(rates: rates);

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: deletedKeys!.Length).IsEqualTo(expected: 1);
		await Assert.That(value: (string)deletedKeys![0]!).IsEqualTo(expected: $"ft_test:rate:latest:{Usd.Value}:{Rub.Value}");
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
