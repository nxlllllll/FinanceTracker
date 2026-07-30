using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class BudgetTests
{
	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.CreateVersion7();
		Guid categoryId = Guid.CreateVersion7();

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
			createdAt: DateTimeOffset.UtcNow,
			userId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
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
		DateOnly sameDate = new DateOnly(year: 2025, month: 1, day: 1);

		Result<Budget, DomainException> result = Budget.Create(
			createdAt: DateTimeOffset.UtcNow,
			userId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: Money.Create(amount: 1000m, currency: Currency.Create(value: "RUB").Value).Value,
			from: sameDate,
			to: sameDate
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

		Result<bool, DomainException> result = budget.ChangeAmount(amount: 0m);

		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task ChangeAmount_WhenInactive_ShouldThrowInactiveBudgetException()
	{
		Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		Result<bool, DomainException> result = budget.ChangeAmount(amount: 5000m);

		await Assert.That(result.IsFailure).IsTrue();
		await Assert.That(result.Error).IsTypeOf<InactiveBudgetException>();
	}

	[Test]
	public async Task Activate_InactiveBudget_ShouldSetIsActive()
	{
		Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		budget.Activate();

		await Assert.That(value: budget.IsActive).IsTrue();
	}

	[Test]
	public async Task Activate_ActiveBudget_ShouldReturnSuccessWithFalse()
	{
		Budget budget = BudgetFactory.Create().Value!;

		Result<bool, DomainException> result = budget.Activate();

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsFalse();
	}

	[Test]
	public async Task Deactivate_ActiveBudget_ShouldClearIsActive()
	{
		Budget budget = BudgetFactory.Create().Value!;

		budget.Deactivate();

		await Assert.That(value: budget.IsActive).IsFalse();
	}

	[Test]
	public async Task Deactivate_InactiveBudget_ShouldReturnSuccessWithFalse()
	{
		Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		Result<bool, DomainException> result = budget.Deactivate();

		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).IsFalse();
	}
}
