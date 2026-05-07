using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
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

        Budget budget = BudgetFactory.Create(userId: userId, categoryId: categoryId).Value!;

        await Assert.That(value: budget.Id).IsNotDefault();
        await Assert.That(value: budget.UserId).IsEqualTo(expected: userId);
        await Assert.That(value: budget.CategoryId).IsEqualTo(expected: categoryId);
        await Assert.That(value: budget.Amount.Amount).IsEqualTo(expected: 10000m);
        await Assert.That(value: budget.Amount.Currency.Value).IsEqualTo(expected: "RUB");
        await Assert.That(value: budget.From).IsEqualTo(expected: new DateOnly(year: 2025, month: 1, day: 1));
        await Assert.That(value: budget.To).IsEqualTo(expected: new DateOnly(year: 2025, month: 1, day: 31));
        await Assert.That(value: budget.CreatedAt).IsNotDefault();
    }

    [Test]
    public async Task Create_WhenEndDateBeforeStartDate_ShouldThrowInvalidBudgetPeriodException()
    {
        Result<Budget, DomainException> result = Budget.Create(
            createdAt: DateTime.UtcNow,
            userId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: Money.Create(amount: 1000m, currency: Currency.Create(value: "RUB").Value).Value,
            from: new DateOnly(year: 2025, month: 1, day: 31),
            to: new DateOnly(year: 2025, month: 1, day: 1)
        );
        
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvalidBudgetPeriodException>();
    }

    [Test]
    public async Task Create_WhenEndDateEqualsStartDate_ShouldThrowInvalidBudgetPeriodException()
    {
        Result<Budget, DomainException> result = Budget.Create(
            createdAt: DateTime.UtcNow,
            userId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: Money.Create(amount: 1000m, currency: Currency.Create(value: "RUB").Value).Value,
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 1)
        );
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvalidBudgetPeriodException>();
    }

    [Test]
    public async Task ChangeAmount_WithValidAmount_ShouldUpdateAmount()
    {
        Budget budget = BudgetFactory.Create().Value!;

        budget.ChangeAmount(amount: 5000m);

        await Assert.That(value: budget.Amount.Amount).IsEqualTo(expected: 5000m);
    }

    [Test]
    public async Task ChangeAmount_WithZeroAmount_ShouldThrowInvalidAmountException()
    {
        Budget budget = BudgetFactory.Create().Value!;

        Result<FinanceTracker.Core.Results.Unit, DomainException> result = budget.ChangeAmount(amount: 0m);
        
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvalidAmountException>();
    }

    [Test]
    public async Task ChangeAmount_WithNegativeAmount_ShouldThrowInvalidAmountException()
    {
        Budget budget = BudgetFactory.Create().Value!;

        Result<FinanceTracker.Core.Results.Unit, DomainException> result = budget.ChangeAmount(amount: -100m);
        
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvalidAmountException>();
    }

    [Test]
    public async Task ChangePeriod_WithValidDates_ShouldUpdatePeriod()
    {
        Budget budget = BudgetFactory.Create().Value!;

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
        Budget budget = BudgetFactory.Create().Value!;

        Result<FinanceTracker.Core.Results.Unit, DomainException> result = budget.ChangePeriod(
            from: new DateOnly(year: 2025, month: 1, day: 31),
            to: new DateOnly(year: 2025, month: 1, day: 1)
        );
        
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvalidBudgetPeriodException>();
    }

    [Test]
    public async Task ChangePeriod_WhenEndDateEqualsStartDate_ShouldThrowInvalidBudgetPeriodException()
    {
        Budget budget = BudgetFactory.Create().Value!;

        Result<FinanceTracker.Core.Results.Unit, DomainException> result = budget.ChangePeriod(
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 1)
        );
        
        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvalidBudgetPeriodException>();
    }
}