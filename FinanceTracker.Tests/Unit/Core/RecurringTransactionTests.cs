using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class RecurringTransactionTests
{
    [Test]
    public async Task Create_WithValidData_ShouldSetCorrectState()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();

        RecurringTransaction rt = RecurringTransactionFactory.Create(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId
        );

        await Assert.That(value: rt.Id).IsNotDefault();
        await Assert.That(value: rt.UserId).IsEqualTo(expected: userId);
        await Assert.That(value: rt.AccountId).IsEqualTo(expected: accountId);
        await Assert.That(value: rt.CategoryId).IsEqualTo(expected: categoryId);
        await Assert.That(value: rt.Amount.Amount).IsEqualTo(expected: 5000m);
        await Assert.That(value: rt.Amount.Currency).IsEqualTo(expected: (Currency)"RUB");
        await Assert.That(value: rt.Direction).IsEqualTo(expected: DirectionType.Debit);
        await Assert.That(value: rt.DayOfMonth).IsEqualTo(expected: 15);
        await Assert.That(value: rt.IsActive).IsTrue();
        await Assert.That(value: rt.LastExecutedAt).IsNull();
        await Assert.That(value: rt.CreatedAt).IsNotDefault();
    }

    [Test]
    public async Task Create_WithDayOfMonthZero_ShouldThrowInvalidDayOfMonthException()
    {
        await Assert.That(func: () => RecurringTransactionFactory.Create(dayOfMonth: 0))
                 .Throws<InvalidDayOfMonthException>();
    }

    [Test]
    public async Task Create_WithDayOfMonth32_ShouldThrowInvalidDayOfMonthException()
    {
        await Assert.That(func: () => RecurringTransactionFactory.Create(dayOfMonth: 32))
                 .Throws<InvalidDayOfMonthException>();
    }

    [Test]
    public async Task Activate_WhenInactive_ShouldSetIsActiveTrue()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create(isActive: false);

        rt.Activate();

        await Assert.That(value: rt.IsActive).IsTrue();
    }

    [Test]
    public async Task Activate_WhenAlreadyActive_ShouldThrowActivatingException()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create(isActive: true);

        await Assert.That(action: rt.Activate).Throws<ActivatingException>();
    }

    [Test]
    public async Task Deactivate_WhenActive_ShouldSetIsActiveFalse()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create(isActive: true);

        rt.Deactivate();

        await Assert.That(value: rt.IsActive).IsFalse();
    }

    [Test]
    public async Task Deactivate_WhenAlreadyInactive_ShouldThrowDeactivatingException()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create(isActive: false);

        await Assert.That(action: rt.Deactivate).Throws<DeactivatingException>();
    }

    [Test]
    public async Task ChangeAmount_WithValidAmount_ShouldUpdateAmount()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create();

        rt.ChangeAmount(amount: 3000m);

        await Assert.That(value: rt.Amount.Amount).IsEqualTo(expected: 3000m);
        await Assert.That(value: rt.Amount.Currency).IsEqualTo(expected: (Currency)"RUB");
    }

    [Test]
    public async Task ChangeAmount_WithNegativeAmount_ShouldThrowInvalidAmountException()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create();

        await Assert.That(action: () => rt.ChangeAmount(amount: -100m)).Throws<InvalidAmountException>();
    }

    [Test]
    public async Task ChangeCurrency_WithValidCurrency_ShouldUpdateCurrency()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create();

        rt.ChangeCurrency(currency: "USD");

        await Assert.That(value: rt.Amount.Currency).IsEqualTo(expected: (Currency)"USD");
        await Assert.That(value: rt.Amount.Amount).IsEqualTo(expected: 5000m);
    }

    [Test]
    public async Task ChangeCurrency_WithInvalidCurrency_ShouldThrowCurrencyException()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create();

        await Assert.That(action: () => rt.ChangeCurrency(currency: String.Empty)).Throws<CurrencyException>();
    }

    [Test]
    public async Task ChangeDayOfMonth_WithValidDay_ShouldUpdateDayOfMonth()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create();

        rt.ChangeDayOfMonth(dayOfMonth: 28);

        await Assert.That(value: rt.DayOfMonth).IsEqualTo(expected: 28);
    }

    [Test]
    public async Task ChangeDayOfMonth_WithZero_ShouldThrowInvalidDayOfMonthException()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create();

        await Assert.That(action: () => rt.ChangeDayOfMonth(dayOfMonth: 0)).Throws<InvalidDayOfMonthException>();
    }

    [Test]
    public async Task ChangeDayOfMonth_With32_ShouldThrowInvalidDayOfMonthException()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create();

        await Assert.That(action: () => rt.ChangeDayOfMonth(dayOfMonth: 32)).Throws<InvalidDayOfMonthException>();
    }

    [Test]
    public async Task MarkExecuted_ShouldSetLastExecutedAt()
    {
        RecurringTransaction rt = RecurringTransactionFactory.Create();
        DateTime executedAt = DateTime.UtcNow;

        rt.MarkExecuted(executedAt: executedAt);

        await Assert.That(value: rt.LastExecutedAt).IsEqualTo(expected: executedAt);
    }
}