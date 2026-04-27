using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.BudgetProgress;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.BudgetProgress;

public sealed class BudgetProgressReadRepository(
    FinanceTrackerContext context
) : IBudgetProgressReadRepository
{
    public async Task<BudgetProgressDto?> GetByBudgetIdAsync(
        Guid budgetId,
        CancellationToken ct = default)
    {
        return await context.BudgetProgresses.AsNoTracking().Where(predicate: p => p.BudgetId == budgetId).Join(
            inner: context.Budgets,
            outerKeySelector: p => p.BudgetId,
            innerKeySelector: b => b.Id,
            resultSelector: (progress, budget) => new BudgetProgressDto(
                BudgetId: progress.BudgetId,
                Spent: progress.Spent,
                Remaining: budget.Amount - progress.Spent,
                Percentage: budget.Amount == 0 ? 0 : progress.Spent / budget.Amount,
                UpdatedAt: progress.UpdatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }
}