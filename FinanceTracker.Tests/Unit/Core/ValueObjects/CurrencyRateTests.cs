using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.ValueObjects;

public sealed class CurrencyRateTests
{
	private static readonly Currency Usd = Currency.Reconstitute(value: "USD");
	private static readonly Currency Eur = Currency.Reconstitute(value: "EUR");
	private static readonly DateOnly Today = new DateOnly(year: 2026, month: 8, day: 11);

	private static Result<CurrencyRate, DomainException> Create(decimal rate)
		=> CurrencyRate.Create(baseCurrency: Usd, target: Eur, rate: rate, date: Today);

	[Test]
	public async Task Create_WithSixDecimals_ShouldKeepTheValueUntouched()
	{
		Result<CurrencyRate, DomainException> result = Create(rate: 1.234567m);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Rate).IsEqualTo(expected: 1.234567m);
	}

	[Test]
	public async Task Create_WithMorePrecisionThanStorageKeeps_ShouldRoundToRateScale()
	{
		Result<CurrencyRate, DomainException> result = Create(rate: 1.23456789m);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Rate).IsEqualTo(expected: 1.234568m);
	}

	[Test]
	public async Task Create_WhenRounding_ShouldUseBankersRounding()
	{
		Result<CurrencyRate, DomainException> result = Create(rate: 1.2345665m);

		await Assert.That(value: result.Value.Rate).IsEqualTo(expected: 1.234566m);
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	[Arguments(-0.000001)]
	public async Task Create_WithANonPositiveRate_ShouldFail(decimal rate)
	{
		Result<CurrencyRate, DomainException> result = Create(rate: rate);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidExchangeRateException>();
	}

	[Test]
	public async Task Create_WithAPositiveRateBelowStoredPrecision_ShouldFailRatherThanBecomeZero()
	{
		Result<CurrencyRate, DomainException> result = Create(rate: 0.0000001m);

		await Assert.That(value: result.IsFailure).IsTrue();
	}

	[Test]
	public async Task Create_ShouldCarryCurrenciesAndDateThrough()
	{
		Result<CurrencyRate, DomainException> result = Create(rate: 1.5m);

		await Assert.That(value: result.Value.Base).IsEqualTo(expected: Usd);
		await Assert.That(value: result.Value.Target).IsEqualTo(expected: Eur);
		await Assert.That(value: result.Value.Date).IsEqualTo(expected: Today);
	}

	[Test]
	public async Task Reconstitute_ShouldNotValidate()
	{
		CurrencyRate rate = CurrencyRate.Reconstitute(baseCurrency: Usd, target: Eur, rate: 0m, date: Today);

		await Assert.That(value: rate.Rate).IsEqualTo(expected: 0m);
	}
}
