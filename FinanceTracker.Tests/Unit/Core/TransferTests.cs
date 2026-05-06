using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class TransferTests
{
    [Test]
    public async Task Create_WithValidData_ShouldSetCorrectState()
    {
        Guid userId = Guid.NewGuid();
        Guid fromAccountId = Guid.NewGuid();
        Guid toAccountId = Guid.NewGuid();

        Transfer transfer = TransferFactory.Create(
            userId: userId,
            fromAccountId: fromAccountId,
            toAccountId: toAccountId,
            amountFrom: 1000m,
            amountTo: 1000m,
            currencyFrom: "RUB",
            currencyTo: "RUB",
            exchangeRate: 1m,
            isRatePending: false
        );

        await Assert.That(value: transfer.Id).IsNotDefault();
        await Assert.That(value: transfer.UserId).IsEqualTo(expected: userId);
        await Assert.That(value: transfer.FromAccountId).IsEqualTo(expected: fromAccountId);
        await Assert.That(value: transfer.ToAccountId).IsEqualTo(expected: toAccountId);
        await Assert.That(value: transfer.AmountFrom.Amount).IsEqualTo(expected: 1000m);
        await Assert.That(value: transfer.AmountTo.Amount).IsEqualTo(expected: 1000m);
        await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 1m);
        await Assert.That(value: transfer.IsRatePending).IsFalse();
        await Assert.That(value: transfer.OccurredAt).IsNotDefault();
    }

    [Test]
    public async Task Create_WithDifferentCurrencies_ShouldSetCorrectAmounts()
    {
        Transfer transfer = TransferFactory.Create(
            amountFrom: 1000m,
            amountTo: 11m,
            currencyFrom: "RUB",
            currencyTo: "USD",
            exchangeRate: 0.011m
        );

        await Assert.That(value: transfer.AmountFrom.Amount).IsEqualTo(expected: 1000m);
        await Assert.That(value: transfer.AmountFrom.Currency.Value).IsEqualTo(expected: "RUB");
        await Assert.That(value: transfer.AmountTo.Amount).IsEqualTo(expected: 11m);
        await Assert.That(value: transfer.AmountTo.Currency.Value).IsEqualTo(expected: "USD");
        await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 0.011m);
    }

    [Test]
    public async Task Create_WithPendingRate_ShouldSetIsRatePendingTrue()
    {
        Transfer transfer = TransferFactory.Create(isRatePending: true);

        await Assert.That(value: transfer.IsRatePending).IsTrue();
    }

    [Test]
    public async Task Create_WithDescription_ShouldSetDescription()
    {
        Transfer transfer = TransferFactory.Create(description: "На отпуск");

        await Assert.That(value: transfer.Description).IsEqualTo(expected: "На отпуск");
    }

    [Test]
    public async Task Create_WithoutDescription_ShouldHaveNullDescription()
    {
        Transfer transfer = TransferFactory.Create(description: null);

        await Assert.That(value: transfer.Description).IsNull();
    }
}