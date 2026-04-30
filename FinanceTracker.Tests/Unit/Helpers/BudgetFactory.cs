using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class BudgetFactory
{
	public static Budget Create(
		Guid? userId = null,
		Guid? categoryId = null)
	{
		return Budget.Create(
			userId: userId ?? Guid.NewGuid(),
			categoryId: categoryId ?? Guid.NewGuid(),
			currency: "RUB",
			amount: 10000m,
			from: new DateOnly(year: 2025, month: 1, day: 1),
			to: new DateOnly(year: 2025, month: 1, day: 31)
		);
	}

	public static BudgetProgressDto CreateProgress(
		Guid? budgetId = null,
		decimal spent = 0m)
	{
		decimal amount = 10000m;
		decimal remaining = amount - spent;
		decimal percentage = amount == 0 ? 0 : spent / amount;

		return new BudgetProgressDto(
			BudgetId: budgetId ?? Guid.NewGuid(),
			Spent: spent,
			Remaining: remaining,
			Percentage: percentage,
			UpdatedAt: DateTime.UtcNow
		);
	}
}