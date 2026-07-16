using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Cache;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CurrencyRateCacheKeysTests
{
	private static readonly Currency Usd = Currency.Reconstitute(value: "USD");
	private static readonly Currency Rub = Currency.Reconstitute(value: "RUB");

	[Test]
	public async Task StableRateKey_ForTwoInstantsInTheSameHour_ShouldBeIdentical()
	{
		DateTimeOffset first = new DateTimeOffset(year: 2025, month: 6, day: 10, hour: 14, minute: 0, second: 5, offset: TimeSpan.Zero);
		DateTimeOffset second = new DateTimeOffset(year: 2025, month: 6, day: 10, hour: 14, minute: 59, second: 58, offset: TimeSpan.Zero);

		string keyFirst = CurrencyRateCacheKeys.StableRateKey(request: new CurrencyStableRateRequest(From: Usd, To: Rub, AsOf: first));
		string keySecond = CurrencyRateCacheKeys.StableRateKey(request: new CurrencyStableRateRequest(From: Usd, To: Rub, AsOf: second));

		await Assert.That(value: keyFirst).IsEqualTo(expected: keySecond)
			.Because(message: "Rates only ever change once a day (CurrencyRateJob, 02:00 UTC) — hourly granularity loses no real distinction and is what lets the cache actually hit.");
	}

	[Test]
	public async Task StableRateKey_ForTwoInstantsInDifferentHours_ShouldDiffer()
	{
		DateTimeOffset first = new DateTimeOffset(year: 2025, month: 6, day: 10, hour: 1, minute: 59, second: 59, offset: TimeSpan.Zero);
		DateTimeOffset second = new DateTimeOffset(year: 2025, month: 6, day: 10, hour: 2, minute: 0, second: 1, offset: TimeSpan.Zero);

		string keyFirst = CurrencyRateCacheKeys.StableRateKey(request: new CurrencyStableRateRequest(From: Usd, To: Rub, AsOf: first));
		string keySecond = CurrencyRateCacheKeys.StableRateKey(request: new CurrencyStableRateRequest(From: Usd, To: Rub, AsOf: second));

		await Assert.That(value: keyFirst).IsNotEqualTo(notExpected: keySecond);
	}

	[Test]
	public async Task StableRateKey_ForDifferentCurrencyPairs_ShouldDiffer()
	{
		DateTimeOffset asOf = new DateTimeOffset(year: 2025, month: 6, day: 10, hour: 14, minute: 0, second: 0, offset: TimeSpan.Zero);

		string usdToRub = CurrencyRateCacheKeys.StableRateKey(request: new CurrencyStableRateRequest(From: Usd, To: Rub, AsOf: asOf));
		string rubToUsd = CurrencyRateCacheKeys.StableRateKey(request: new CurrencyStableRateRequest(From: Rub, To: Usd, AsOf: asOf));

		await Assert.That(value: usdToRub).IsNotEqualTo(notExpected: rubToUsd);
	}

	[Test]
	public async Task StableRateKey_ShouldBeStableAcrossCalls()
	{
		DateTimeOffset asOf = new DateTimeOffset(year: 2025, month: 6, day: 10, hour: 14, minute: 30, second: 0, offset: TimeSpan.Zero);
		CurrencyStableRateRequest request = new CurrencyStableRateRequest(From: Usd, To: Rub, AsOf: asOf);

		string keyOne = CurrencyRateCacheKeys.StableRateKey(request: request);
		string keyTwo = CurrencyRateCacheKeys.StableRateKey(request: request);

		await Assert.That(value: keyOne).IsEqualTo(expected: keyTwo);
	}
}
