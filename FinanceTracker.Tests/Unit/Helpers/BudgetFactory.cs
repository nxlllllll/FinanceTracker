using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class BudgetFactory
{
	public static Result<Budget, DomainException> Create(
		Guid? userId = null,
		Guid? categoryId = null)
	{
		return Budget.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId ?? Guid.CreateVersion7(),
			categoryId: categoryId ?? Guid.CreateVersion7(),
			amount: Money.Create(amount: 10000m, currency: Currency.Create(value: "RUB").Value).Value,
			from: new DateOnly(year: 2025, month: 1, day: 1),
			to: new DateOnly(year: 2025, month: 1, day: 31)
		);
	}

	public static BudgetReadModel CreateReadModel(
		Guid? id = null,
		Guid? userId = null,
		Guid? categoryId = null,
		bool isActive = true)
	{
		return new BudgetReadModel(
			Id: id ?? Guid.CreateVersion7(),
			UserId: userId ?? Guid.CreateVersion7(),
			CategoryId: categoryId ?? Guid.CreateVersion7(),
			Amount: Money.Create(amount: 10000m, currency: Currency.Create(value: "RUB").Value).Value,
			From: new DateOnly(year: 2025, month: 1, day: 1),
			To: new DateOnly(year: 2025, month: 1, day: 31),
			IsActive: isActive,
			CreatedAt: FakeDateProvider.Default.UtcNow
		);
	}

	public static BudgetProgress CreateProgress(
		Guid? budgetId = null,
		decimal spent = 0m)
	{
		const decimal amount = 10000m;
		decimal remaining = amount - spent;
		decimal percentage = amount == 0 ? 0 : spent / amount;

		return new BudgetProgress(
			BudgetId: budgetId ?? Guid.CreateVersion7(),
			Spent: spent,
			Remaining: remaining,
			Percentage: percentage,
			UpdatedAt: FakeDateProvider.Default.UtcNow
		);
	}
}
