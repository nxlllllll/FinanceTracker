using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetRepository(
	FinanceTrackerContext context
) : IBudgetRepository
{
	public async Task<Core.Domains.Budget.Budget?> GetByIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Budgets.AsNoTracking().Where(predicate: b => b.Id == budgetId && b.UserId == userId)
			.Select(selector: b => Core.Domains.Budget.Budget.Reconstitute(
				id: b.Id,
				userId: b.UserId,
				categoryId: b.CategoryId,
				amount: Money.Reconstitute(amount: b.Amount, currency: b.Currency),
				isActive: b.IsActive,
				from: b.From,
				to: b.To,
				rowVersion: b.RowVersion,
				createdAt: b.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}
}