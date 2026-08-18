using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetProgressReadRepository(
	FinanceTrackerContext context
) : IBudgetProgressReadRepository
{
	public async Task<BudgetProgress?> GetByBudgetIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.BudgetProgresses.AsNoTracking().Where(predicate: p => p.BudgetId == budgetId).Join(
			inner: context.Budgets.Where(predicate: b => b.UserId == userId),
			outerKeySelector: p => p.BudgetId,
			innerKeySelector: b => b.Id,
			resultSelector: (progress, budget) => new BudgetProgress(
				BudgetId: progress.BudgetId,
				Spent: progress.Spent,
				Remaining: budget.Amount - progress.Spent,
				Percentage: budget.Amount == 0 ? 0 : progress.Spent / budget.Amount * 100,
				UpdatedAt: progress.UpdatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}
}
