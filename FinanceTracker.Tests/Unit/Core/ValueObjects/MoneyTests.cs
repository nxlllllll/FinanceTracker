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
        await Assert.That(value: result.Value.Currency.Value).IsEqualTo(expected: "USD");
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
        await Assert.That(value: money.Currency.Value).IsEqualTo(expected: "USD");
    }

    [Test]
    public async Task OperatorPlus_ShouldIncreaseAmount()
    {
        Money money = Money.Reconstitute(amount: 100m, currency: Rub);

        Money result = money + 50m;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 150m);
        await Assert.That(value: result.Currency.Value).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task OperatorPlus_ShouldPreserveCurrency()
    {
        Money money = Money.Reconstitute(amount: 100m, currency: Usd);

        Money result = money + 1m;

        await Assert.That(value: result.Currency.Value).IsEqualTo(expected: "USD");
    }

    [Test]
    public async Task OperatorMinus_ShouldDecreaseAmount()
    {
        Money money = Money.Reconstitute(amount: 100m, currency: Rub);

        Money result = money - 30m;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 70m);
    }

    [Test]
    public async Task OperatorMinus_ToExactZero_ShouldSucceed()
    {
        Money money = Money.Reconstitute(amount: 100m, currency: Rub);

        Money result = money - 100m;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 0m);
    }

    [Test]
    public async Task OperatorMinus_BelowZero_ShouldResultInNegativeAmount()
    {
        Money money = Money.Reconstitute(amount: 50m, currency: Rub);

        Money result = money - 100m;

        await Assert.That(value: result.Amount).IsEqualTo(expected: -50m);
    }

    [Test]
    public async Task OperatorMultiply_ShouldScaleAmount()
    {
        Money money = Money.Reconstitute(amount: 100m, currency: Rub);

        Money result = money * 90m;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 9000m);
    }

    [Test]
    public async Task OperatorMultiply_ByZero_ShouldReturnZeroAmount()
    {
        Money money = Money.Reconstitute(amount: 100m, currency: Rub);

        Money result = money * 0m;

        await Assert.That(value: result.Amount).IsEqualTo(expected: 0m);
    }

    [Test]
    public async Task OperatorMultiply_ShouldPreserveCurrency()
    {
        Money money = Money.Reconstitute(amount: 100m, currency: Usd);

        Money result = money * 2m;

        await Assert.That(value: result.Currency.Value).IsEqualTo(expected: "USD");
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