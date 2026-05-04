using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
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
			CreatedAt = dateProvider.UtcNow
		}, cancellationToken: ct);

		await context.BudgetProgresses.AddAsync(entity: new BudgetProgressEntity()
		{
			BudgetId = budget.Id,
			Spent = 0,
			UpdatedAt = dateProvider.UtcNow
		}, cancellationToken: ct);
		
		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task ChangeAmountAsync(
		Guid budgetId,
		decimal amount,
		CancellationToken ct = default)
	{
		await context.Budgets.Where(predicate: budget => budget.Id == budgetId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(
				propertyExpression: budget => budget.Amount,
				valueExpression: amount
			),
			cancellationToken: ct
		);
	}

	public async Task ChangePeriodAsync(
		Guid budgetId,
		DateOnly from,
		DateOnly to,
		CancellationToken ct = default)
	{
		await context.Budgets.Where(predicate: budget => budget.Id == budgetId).ExecuteUpdateAsync(setPropertyCalls: builder => 
			builder.SetProperty(
				propertyExpression: budget => budget.From,
				valueExpression: from
			).SetProperty(
				propertyExpression: budget => budget.To,
				valueExpression: to
			),
			cancellationToken: ct
		);
	}

	public async Task DeleteAsync(
		Guid budgetId,
		CancellationToken ct = default
	) => await context.Budgets.Where(predicate: b => b.Id == budgetId).ExecuteDeleteAsync(cancellationToken: ct);
}