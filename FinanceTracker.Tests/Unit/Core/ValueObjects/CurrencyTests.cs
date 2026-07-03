using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.ValueObjects;

public sealed class CurrencyTests
{
	[Test]
	public async Task Create_WithValidCode_ShouldSucceed()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "RUB");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: "RUB");
	}

	[Test]
	public async Task Create_WithLowercaseCode_ShouldNormalizeToUppercase()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "usd");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: "USD");
	}

	[Test]
	public async Task Create_WithMixedCaseCode_ShouldNormalizeToUppercase()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "eUr");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: "EUR");
	}

	[Test]
	public async Task Create_WithLeadingAndTrailingSpaces_ShouldTrimAndNormalize()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "  usd  ");

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: "USD");
	}

	[Test]
	public async Task Create_WithEmptyString_ShouldReturnFailure()
	{
		Result<Currency, DomainException> result = Currency.Create(value: String.Empty);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
	}

	[Test]
	public async Task Create_WithWhitespaceOnly_ShouldReturnFailure()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "   ");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
	}

	[Test]
	public async Task Create_WithTwoCharacters_ShouldReturnFailure()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "RU");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
	}

	[Test]
	public async Task Create_WithFourCharacters_ShouldReturnFailure()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "RUBL");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
	}

	[Test]
	public async Task Create_WithDigits_ShouldReturnFailure()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "R1B");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
	}

	[Test]
	public async Task Create_WithSpecialCharacters_ShouldReturnFailure()
	{
		Result<Currency, DomainException> result = Currency.Create(value: "R$B");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
	}

	[Test]
	public async Task Reconstitute_ShouldBypassValidation()
	{
		Currency currency = Currency.Reconstitute(value: "RUB");

		await Assert.That(value: currency.Value).IsEqualTo(expected: "RUB");
	}

	[Test]
	public async Task ImplicitOperator_ToString_ShouldReturnValue()
	{
		Currency currency = Currency.Reconstitute(value: "USD");

		string result = currency;

		await Assert.That(value: result).IsEqualTo(expected: "USD");
	}

	[Test]
	public async Task ToString_ShouldReturnCode()
	{
		Currency currency = Currency.Reconstitute(value: "EUR");

		await Assert.That(value: currency.ToString()).IsEqualTo(expected: "EUR");
	}

	[Test]
	public async Task Equality_SameCode_ShouldBeEqual()
	{
		Currency a = Currency.Reconstitute(value: "RUB");
		Currency b = Currency.Reconstitute(value: "RUB");

		await Assert.That(value: a).IsEqualTo(expected: b);
	}

	[Test]
	public async Task Equality_DifferentCode_ShouldNotBeEqual()
	{
		Currency a = Currency.Reconstitute(value: "RUB");
		Currency b = Currency.Reconstitute(value: "USD");

		await Assert.That(value: a).IsNotEqualTo(notExpected: b);
	}

	[Test]
	[Arguments("RUB")]
	[Arguments("USD")]
	[Arguments("EUR")]
	[Arguments("GBP")]
	[Arguments("JPY")]
	[Arguments("CNY")]
	public async Task Create_WithKnownCurrencyCodes_ShouldSucceed(string code)
	{
		Result<Currency, DomainException> result = Currency.Create(value: code);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.Value).IsEqualTo(expected: code);
	}

	[Test]
	[Arguments("")]
	[Arguments("RU")]
	[Arguments("RUBL")]
	[Arguments("123")]
	[Arguments("R1B")]
	public async Task Create_WithInvalidCodes_ShouldReturnFailure(string code)
	{
		Result<Currency, DomainException> result = Currency.Create(value: code);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CurrencyException>();
	}
}
