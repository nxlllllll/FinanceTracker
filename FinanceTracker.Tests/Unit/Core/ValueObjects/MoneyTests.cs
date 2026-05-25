using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.ValueObjects;

public sealed class MoneyTests
{
    private static Currency Rub => Currency.Reconstitute(value: "RUB");
    private static Currency Usd => Currency.Reconstitute(value: "USD");

    [Test]
    public async Task Create_WithZeroAmount_ShouldSucceed()
    {
        Result<Money, DomainException> result = Money.Create(amount: 0m, currency: Rub);

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value.Amount).IsEqualTo(expected: 0m);
    }

    [Test]
    public async Task Create_WithPositiveAmount_ShouldSucceed()
    {
        Result<Money, DomainException> result = Money.Create(amount: 100.50m, currency: Rub);

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value.Amount).IsEqualTo(expected: 100.50m);
    }

    [Test]
    public async Task Create_WithNegativeAmount_ShouldReturnFailure()
    {
        Result<Money, DomainException> result = Money.Create(amount: -1m, currency: Rub);

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
    }

    [Test]
    public async Task Create_ShouldPreserveCurrency()
    {
        Result<Money, DomainException> result = Money.Create(amount: 100m, currency: Usd);

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value.Currency).IsEqualTo(expected: Usd);
    }

    [Test]
    public async Task Positive_WithPositiveAmount_ShouldSucceed()
    {
        Result<Money, DomainException> result = Money.Positive(amount: 0.01m, currency: Rub);

        await Assert.That(value: result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Positive_WithZeroAmount_ShouldReturnFailure()
    {
        Result<Money, DomainException> result = Money.Positive(amount: 0m, currency: Rub);

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
    }

    [Test]
    public async Task Positive_WithNegativeAmount_ShouldReturnFailure()
    {
        Result<Money, DomainException> result = Money.Positive(amount: -50m, currency: Rub);

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
    }

    [Test]
    public async Task Reconstitute_ShouldBypassValidation_AndAllowZero()
    {
        Money money = Money.Reconstitute(amount: 0m, currency: Rub);

        await Assert.That(value: money.Amount).IsEqualTo(expected: 0m);
    }

    [Test]
    public async Task Reconstitute_ShouldPreserveExactAmount()
    {
        Money money = Money.Reconstitute(amount: 9999.99m, currency: Usd);

        await Assert.That(value: money.Amount).IsEqualTo(expected: 9999.99m);
        await Assert.That(value: money.Currency).IsEqualTo(expected: Usd);
    }

    [Test]
    public async Task OperatorPlus_ShouldIncreaseAmount()
    {
        Money left = Money.Reconstitute(amount: 100m, currency: Rub);
        Money right = Money.Reconstitute(amount: 50m, currency: Rub);

        Money result = left + right;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 150m);
        await Assert.That(value: result.Currency).IsEqualTo(expected: Rub);
    }

    [Test]
    public async Task OperatorPlus_ShouldPreserveCurrency()
    {
        Money left = Money.Reconstitute(amount: 100m, currency: Usd);
        Money right = Money.Reconstitute(amount: 1m, currency: Usd);

        Money result = left + right;

        await Assert.That(value: result.Currency).IsEqualTo(expected: Usd);
    }

    [Test]
    public async Task OperatorMinus_ShouldDecreaseAmount()
    {
        Money left = Money.Reconstitute(amount: 100m, currency: Rub);
        Money right = Money.Reconstitute(amount: 30m, currency: Rub);

        Money result = left - right;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 70m);
    }

    [Test]
    public async Task OperatorMinus_ToExactZero_ShouldSucceed()
    {
        Money left = Money.Reconstitute(amount: 100m, currency: Rub);
        Money right = Money.Reconstitute(amount: 100m, currency: Rub);

        Money result = left - right;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 0m);
    }

    [Test]
    public async Task OperatorMinus_BelowZero_ShouldResultInNegativeAmount()
    {
        Money left = Money.Reconstitute(amount: 50m, currency: Rub);
        Money right = Money.Reconstitute(amount: 100m, currency: Rub);

        Money result = left - right;

        await Assert.That(value: result.Amount).IsEqualTo(expected: -50m);
    }

    [Test]
    public async Task OperatorPlusMoney_SameCurrency_ShouldSumAmounts()
    {
        Money left = Money.Reconstitute(amount: 100m, currency: Rub);
        Money right = Money.Reconstitute(amount: 50m, currency: Rub);

        Money result = left + right;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 150m);
        await Assert.That(value: result.Currency).IsEqualTo(expected: Rub);
    }

    [Test]
    public async Task OperatorPlusMoney_DifferentCurrencies_ShouldThrowCurrencyException()
    {
        Money left = Money.Reconstitute(amount: 100m, currency: Rub);
        Money right = Money.Reconstitute(amount: 50m, currency: Usd);

        Assert.Throws<CurrencyException>(action: () => _ = left + right);
    }

    [Test]
    public async Task OperatorMinusMoney_SameCurrency_ShouldSubtractAmounts()
    {
        Money left = Money.Reconstitute(amount: 100m, currency: Rub);
        Money right = Money.Reconstitute(amount: 30m, currency: Rub);

        Money result = left - right;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 70m);
        await Assert.That(value: result.Currency).IsEqualTo(expected: Rub);
    }

    [Test]
    public async Task OperatorMinusMoney_DifferentCurrencies_ShouldThrowCurrencyException()
    {
        Money left = Money.Reconstitute(amount: 100m, currency: Rub);
        Money right = Money.Reconstitute(amount: 30m, currency: Usd);

        Assert.Throws<CurrencyException>(action: () => _ = left - right);
    }
    [Test]
    public async Task ToString_ShouldReturnAmountAndCurrency()
    {
        Money money = Money.Reconstitute(amount: 100.50m, currency: Rub);

        await Assert.That(value: money.ToString()).IsEqualTo(expected: "100.50 RUB");
    }
    
    [Test]
    public async Task Equality_SameAmountAndCurrency_ShouldBeEqual()
    {
        Money a = Money.Reconstitute(amount: 100m, currency: Rub);
        Money b = Money.Reconstitute(amount: 100m, currency: Rub);

        await Assert.That(value: a).IsEqualTo(expected: b);
    }

    [Test]
    public async Task Equality_DifferentAmount_ShouldNotBeEqual()
    {
        Money a = Money.Reconstitute(amount: 100m, currency: Rub);
        Money b = Money.Reconstitute(amount: 200m, currency: Rub);

        await Assert.That(value: a).IsNotEqualTo(notExpected: b);
    }

    [Test]
    public async Task Equality_DifferentCurrency_ShouldNotBeEqual()
    {
        Money a = Money.Reconstitute(amount: 100m, currency: Rub);
        Money b = Money.Reconstitute(amount: 100m, currency: Usd);

        await Assert.That(value: a).IsNotEqualTo(notExpected: b);
    }
}
