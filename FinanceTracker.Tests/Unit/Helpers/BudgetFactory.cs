using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class BudgetFactory
{
	public static Result<Budget, DomainException> Create(
		Guid? userId = null,
		Guid? categoryId = null)
	{
		Result<Budget, DomainException> result = Budget.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId ?? Guid.CreateVersion7(),
			categoryId: categoryId ?? Guid.CreateVersion7(),
			amount: Money.Create(amount: 10000m, currency: Currency.Create(value: "RUB").Value).Value,
			from: new DateOnly(year: 2025, month: 1, day: 1),
			to: new DateOnly(year: 2025, month: 1, day: 31)
		);
		
		return result;
	}

	public static BudgetProgressDto CreateProgress(
		Guid? budgetId = null,
		decimal spent = 0m)
	{
		decimal amount = 10000m;
		decimal remaining = amount - spent;
		decimal percentage = amount == 0 ? 0 : spent / amount;

		return new BudgetProgressDto(
			BudgetId: budgetId ?? Guid.CreateVersion7(),
			Spent: spent,
			Remaining: remaining,
			Percentage: percentage,
			UpdatedAt: FakeDateProvider.Default.UtcNow
		);
	}
}