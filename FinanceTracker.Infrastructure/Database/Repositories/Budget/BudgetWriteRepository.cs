using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : IBudgetWriteRepository
{
	public async Task CreateAsync(
		Core.Domains.Budget.Budget budget,
		CancellationToken ct = default)
	{
		await context.Budgets.AddAsync(entity: new BudgetEntity()
		{
			Id = budget.Id,
			UserId = budget.UserId,
			CategoryId = budget.CategoryId,
			Amount = budget.Amount.Amount,
			Currency = budget.Amount.Currency,
			From = budget.From,
			To = budget.To,
			IsActive = true,
			CreatedAt = dateProvider.UtcNow
		}, cancellationToken: ct);

		await context.BudgetProgresses.AddAsync(entity: new BudgetProgressEntity()
		{
			BudgetId = budget.Id,
			Spent = 0,
			UpdatedAt = dateProvider.UtcNow
		}, cancellationToken: ct);
	}

	public async Task ChangeAmountAsync(
		Guid budgetId,
		decimal amount,
		CancellationToken ct = default)
	{
		await context.Budgets.Where(predicate: b => b.Id == budgetId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: b => b.Amount, valueExpression: amount),
			cancellationToken: ct
		);
	}

	public async Task ChangePeriodAsync(
		Guid budgetId,
		DateOnly from,
		DateOnly to,
		CancellationToken ct = default)
	{
		await context.Budgets.Where(predicate: b => b.Id == budgetId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: b => b.From, valueExpression: from)
				.SetProperty(propertyExpression: b => b.To, valueExpression: to),
			cancellationToken: ct
		);
	}

	public async Task ActivateAsync(
		Guid budgetId,
		CancellationToken ct = default)
	{
		await context.Budgets.Where(predicate: b => b.Id == budgetId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: b => b.IsActive, valueExpression: true),
			cancellationToken: ct
		);
	}
	
	public async Task DeactivateAsync(
		Guid budgetId,
		CancellationToken ct = default)
	{
		await context.Budgets.Where(predicate: b => b.Id == budgetId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: b => b.IsActive, valueExpression: false),
			cancellationToken: ct
		);
	}

	public async Task DeactivateByCategoryIdAsync(
		Guid categoryId,
		CancellationToken ct = default)
	{
		await context.Budgets.Where(predicate: b => b.CategoryId == categoryId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: b => b.IsActive, valueExpression: false),
			cancellationToken: ct
		);
	}
}