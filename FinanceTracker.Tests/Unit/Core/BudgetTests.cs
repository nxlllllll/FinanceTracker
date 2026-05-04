using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class BudgetTests
{
    [Test]
    public async Task Create_WithValidData_ShouldSetCorrectState()
    {
        Guid userId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();

        Budget budget = BudgetFactory.Create(userId: userId, categoryId: categoryId);

        await Assert.That(value: budget.Id).IsNotDefault();
        await Assert.That(value: budget.UserId).IsEqualTo(expected: userId);
        await Assert.That(value: budget.CategoryId).IsEqualTo(expected: categoryId);
        await Assert.That(value: budget.Amount.Amount).IsEqualTo(expected: 10000m);
        await Assert.That(value: budget.Amount.Currency).IsEqualTo(expected: (Currency)"RUB");
        await Assert.That(value: budget.From).IsEqualTo(expected: new DateOnly(year: 2025, month: 1, day: 1));
        await Assert.That(value: budget.To).IsEqualTo(expected: new DateOnly(year: 2025, month: 1, day: 31));
        await Assert.That(value: budget.CreatedAt).IsNotDefault();
    }

    [Test]
    public async Task Create_WhenEndDateBeforeStartDate_ShouldThrowInvalidBudgetPeriodException()
        => await Assert.That(func: () => Budget.Create(
            createdAt: DateTime.UtcNow,
            userId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: new Money(amount: 1000m, currency: "RUB"),
            from: new DateOnly(year: 2025, month: 1, day: 31),
            to: new DateOnly(year: 2025, month: 1, day: 1)
        )).Throws<InvalidBudgetPeriodException>();

    [Test]
    public async Task Create_WhenEndDateEqualsStartDate_ShouldThrowInvalidBudgetPeriodException()
        => await Assert.That(func: () => Budget.Create(
            createdAt: DateTime.UtcNow,
            userId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: new Money(amount: 1000m, currency: "RUB"),
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 1)
        )).Throws<InvalidBudgetPeriodException>();

    [Test]
    public async Task ChangeAmount_WithValidAmount_ShouldUpdateAmount()
    {
        Budget budget = BudgetFactory.Create();

        budget.ChangeAmount(amount: 5000m);

        await Assert.That(value: budget.Amount.Amount).IsEqualTo(expected: 5000m);
    }

    [Test]
    public async Task ChangeAmount_WithZeroAmount_ShouldThrowInvalidAmountException()
    {
        Budget budget = BudgetFactory.Create();

        await Assert.That(action: () => budget.ChangeAmount(amount: 0m)).Throws<InvalidAmountException>();
    }

    [Test]
    public async Task ChangeAmount_WithNegativeAmount_ShouldThrowInvalidAmountException()
    {
        Budget budget = BudgetFactory.Create();

        await Assert.That(action: () => budget.ChangeAmount(amount: -100m)).Throws<InvalidAmountException>();
    }

    [Test]
    public async Task ChangePeriod_WithValidDates_ShouldUpdatePeriod()
    {
        Budget budget = BudgetFactory.Create();

        budget.ChangePeriod(
            from: new DateOnly(year: 2025, month: 2, day: 1),
            to: new DateOnly(year: 2025, month: 2, day: 28)
        );

        await Assert.That(value: budget.From).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 1));
        await Assert.That(value: budget.To).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 28));
    }

    [Test]
    public async Task ChangePeriod_WhenEndDateBeforeStartDate_ShouldThrowInvalidBudgetPeriodException()
    {
        Budget budget = BudgetFactory.Create();

        await Assert.That(action: () => budget.ChangePeriod(
            from: new DateOnly(year: 2025, month: 1, day: 31),
            to: new DateOnly(year: 2025, month: 1, day: 1)
        )).Throws<InvalidBudgetPeriodException>();
    }

    [Test]
    public async Task ChangePeriod_WhenEndDateEqualsStartDate_ShouldThrowInvalidBudgetPeriodException()
    {
        Budget budget = BudgetFactory.Create();

        await Assert.That(action: () => budget.ChangePeriod(
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 1)
        )).Throws<InvalidBudgetPeriodException>();
    }
}