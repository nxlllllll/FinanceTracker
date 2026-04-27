using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class BudgetFactory
{
	public static BudgetDto Create(
		Guid? userId = null,
		Guid? categoryId = null)
	{
		return new BudgetDto(
			Id: Guid.NewGuid(),
			UserId: userId ?? Guid.NewGuid(),
			CategoryId: categoryId ?? Guid.NewGuid(),
			Currency: "RUB",
			Amount: 10000m,
			From: new DateOnly(year: 2025, month: 1, day: 1),
			To: new DateOnly(year: 2025, month: 1, day: 31),
			CreatedAt: DateTime.UtcNow
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