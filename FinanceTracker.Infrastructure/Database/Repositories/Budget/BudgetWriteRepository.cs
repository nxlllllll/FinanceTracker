using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetWriteRepository(
	FinanceTrackerContext context
) : IBudgetWriteRepository
{
	public async Task CreateAsync(
		Guid budgetId,
		Guid userId,
		Guid categoryId,
		string currency,
		decimal amount,
		DateOnly from,
		DateOnly to,
		CancellationToken ct = default)
	{
		await context.Budgets.AddAsync(entity: new BudgetEntity()
		{
			Id =  budgetId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			Currency = currency,
			From = from,
			To = to,
			CreatedAt = DateTime.UtcNow
		}, cancellationToken: ct);

		await context.BudgetProgresses.AddAsync(entity: new BudgetProgressEntity()
		{
			BudgetId = budgetId,
			Spent = 0,
			UpdatedAt = DateTime.UtcNow
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