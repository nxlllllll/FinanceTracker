using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Currency;

public sealed class CurrencyRateReadRepositoryTests : DatabaseFixture
{
	private CurrencyRateReadRepository _readRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_readRepository = new CurrencyRateReadRepository(context: Context);
		_currencyBuilder = new CurrencyBuilder(context: Context);
	}

	private async Task SeedRateAsync(
		Core.ValueObjects.Currency baseCode,
		Core.ValueObjects.Currency targetCode,
		decimal rate,
		DateOnly date,
		DateTimeOffset? createdAt = null)
	{
		await Context.CurrencyRates.AddAsync(entity: new CurrencyRateEntity()
		{
			BaseCode = baseCode,
			TargetCode = targetCode,
			Rate = rate,
			ActualAt = date,
			CreatedAt = createdAt ?? DateTimeOffset.UtcNow
		});
		await Context.SaveChangesAsync();
	}

	[Test]
	public async Task GetRateAsync_WhenSameCurrency_ShouldReturnOne()
	{
		decimal? result = await _readRepository.GetRateAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)
		);

		await Assert.That(value: result).IsEqualTo(expected: 1m);
	}

	[Test]
	public async Task GetRateAsync_WhenRateExists_ShouldReturnRate()
	{
		await _currencyBuilder.CreateAsync(code: "USD");
		await _currencyBuilder.CreateAsync(code: "RUB");

		DateOnly date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
		await SeedRateAsync(
			baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			rate: 90m,
			date: date
		);

		decimal? result = await _readRepository.GetRateAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			date: date
		);

		await Assert.That(value: result).IsEqualTo(expected: 90m);
	}

	[Test]
	public async Task GetRateAsync_WhenRateNotExists_ShouldReturnNull()
	{
		decimal? result = await _readRepository.GetRateAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetLatestRateAsync_WhenSameCurrency_ShouldReturnOne()
	{
		decimal? result = await _readRepository.GetLatestRateAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value
		);

		await Assert.That(value: result).IsEqualTo(expected: 1m);
	}

	[Test]
	public async Task GetLatestRateAsync_WhenMultipleRatesExist_ShouldReturnLatest()
	{
		await _currencyBuilder.CreateAsync(code: "USD");
		await _currencyBuilder.CreateAsync(code: "RUB");

		await SeedRateAsync(
			baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			rate: 85m,
			date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(days: -2).UtcDateTime)
		);
		await SeedRateAsync(
			baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			rate: 90m,
			date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(days: -1).UtcDateTime)
		);
		await SeedRateAsync(
			baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			rate: 92m,
			date: DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)
		);

		decimal? result = await _readRepository.GetLatestRateAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value
		);

		await Assert.That(value: result).IsEqualTo(expected: 92m);
	}

	[Test]
	public async Task GetLatestRateAsync_WhenNoRateExists_ShouldReturnNull()
	{
		decimal? result = await _readRepository.GetLatestRateAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_WhenSameCurrency_ShouldReturnOne()
	{
		decimal? result = await _readRepository.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			asOf: DateTimeOffset.UtcNow
		);

		await Assert.That(value: result).IsEqualTo(expected: 1m);
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_WhenRateWasCreatedBeforeAsOf_ShouldReturnRate()
	{
		await _currencyBuilder.CreateAsync(code: "USD");
		await _currencyBuilder.CreateAsync(code: "RUB");

		DateTimeOffset createdAt = new DateTimeOffset(year: 2025, month: 1, day: 9, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero);
		await SeedRateAsync(
			baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			rate: 90m,
			date: DateOnly.FromDateTime(createdAt.UtcDateTime),
			createdAt: createdAt
		);

		decimal? result = await _readRepository.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			asOf: createdAt.AddHours(hours: 1)
		);

		await Assert.That(value: result).IsEqualTo(expected: 90m);
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_WhenOnlyRateWasCreatedAfterAsOf_ShouldReturnNull()
	{
		await _currencyBuilder.CreateAsync(code: "USD");
		await _currencyBuilder.CreateAsync(code: "RUB");

		DateTimeOffset createdAt = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero);
		await SeedRateAsync(
			baseCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			rate: 90m,
			date: DateOnly.FromDateTime(createdAt.UtcDateTime),
			createdAt: createdAt
		);

		decimal? result = await _readRepository.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: Core.ValueObjects.Currency.Create(value: "USD").Value,
			targetCurrencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			asOf: createdAt.AddHours(hours: -9)
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetRateKnownAtOrBeforeAsync_WhenMultipleRatesExist_ShouldIgnoreOnesRecordedAfterAsOf()
	{
		await _currencyBuilder.CreateAsync(code: "USD");
		await _currencyBuilder.CreateAsync(code: "RUB");

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		DateTimeOffset day1 = new DateTimeOffset(year: 2025, month: 1, day: 9, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset day2 = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset day3 = new DateTimeOffset(year: 2025, month: 1, day: 11, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero);

		await SeedRateAsync(baseCode: usd, targetCode: rub, rate: 85m, date: DateOnly.FromDateTime(day1.UtcDateTime), createdAt: day1);
		await SeedRateAsync(baseCode: usd, targetCode: rub, rate: 90m, date: DateOnly.FromDateTime(day2.UtcDateTime), createdAt: day2);
		await SeedRateAsync(baseCode: usd, targetCode: rub, rate: 95m, date: DateOnly.FromDateTime(day3.UtcDateTime), createdAt: day3);

		DateTimeOffset transactionOccurredAt = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 9, minute: 0, second: 0, offset: TimeSpan.Zero);

		decimal? result = await _readRepository.GetRateKnownAtOrBeforeAsync(
			baseCurrencyCode: usd,
			targetCurrencyCode: rub,
			asOf: transactionOccurredAt
		);

		await Assert.That(value: result).IsEqualTo(expected: 85m);
	}

	[Test]
	public async Task GetRatesKnownAtOrBeforeBatchAsync_WhenEmpty_ShouldReturnEmptyDictionary()
	{
		Dictionary<CurrencyStableRateRequest, decimal> result = await _readRepository.GetRatesKnownAtOrBeforeBatchAsync(requests: []);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetRatesKnownAtOrBeforeBatchAsync_WhenSameCurrency_ShouldReturnOneWithoutQuerying()
	{
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;
		CurrencyStableRateRequest request = new CurrencyStableRateRequest(From: rub, To: rub, AsOf: DateTimeOffset.UtcNow);

		Dictionary<CurrencyStableRateRequest, decimal> result = await _readRepository.GetRatesKnownAtOrBeforeBatchAsync(requests: [request]);

		await Assert.That(value: result[request]).IsEqualTo(expected: 1m);
	}

	[Test]
	public async Task GetRatesKnownAtOrBeforeBatchAsync_WhenDifferentPairsWithDifferentAsOf_ShouldResolveEachIndependently()
	{
		await _currencyBuilder.CreateAsync(code: "USD");
		await _currencyBuilder.CreateAsync(code: "EUR");
		await _currencyBuilder.CreateAsync(code: "RUB");

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency eur = Core.ValueObjects.Currency.Create(value: "EUR").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		DateTimeOffset usdRubOld = new DateTimeOffset(year: 2025, month: 1, day: 9, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset usdRubNew = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset eurRubOnly = new DateTimeOffset(year: 2025, month: 1, day: 5, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

		await SeedRateAsync(baseCode: usd, targetCode: rub, rate: 85m, date: DateOnly.FromDateTime(usdRubOld.UtcDateTime), createdAt: usdRubOld);
		await SeedRateAsync(baseCode: usd, targetCode: rub, rate: 90m, date: DateOnly.FromDateTime(usdRubNew.UtcDateTime), createdAt: usdRubNew);
		await SeedRateAsync(baseCode: eur, targetCode: rub, rate: 100m, date: DateOnly.FromDateTime(eurRubOnly.UtcDateTime), createdAt: eurRubOnly);

		DateTimeOffset usdAsOf = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 9, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset eurAsOf = new DateTimeOffset(year: 2025, month: 2, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		CurrencyStableRateRequest usdRequest = new CurrencyStableRateRequest(From: usd, To: rub, AsOf: usdAsOf);
		CurrencyStableRateRequest eurRequest = new CurrencyStableRateRequest(From: eur, To: rub, AsOf: eurAsOf);

		Dictionary<CurrencyStableRateRequest, decimal> result = await _readRepository.GetRatesKnownAtOrBeforeBatchAsync(
			requests: [usdRequest, eurRequest]
		);

		await Assert.That(value: result[usdRequest]).IsEqualTo(expected: 85m);
		await Assert.That(value: result[eurRequest]).IsEqualTo(expected: 100m);
	}

	[Test]
	public async Task GetRatesKnownAtOrBeforeBatchAsync_WhenNoRateExistsBeforeAsOf_ShouldOmitFromResult()
	{
		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;
		CurrencyStableRateRequest request = new CurrencyStableRateRequest(From: usd, To: rub, AsOf: DateTimeOffset.UtcNow);

		Dictionary<CurrencyStableRateRequest, decimal> result = await _readRepository.GetRatesKnownAtOrBeforeBatchAsync(requests: [request]);

		await Assert.That(value: result.ContainsKey(key: request)).IsFalse();
	}
}
