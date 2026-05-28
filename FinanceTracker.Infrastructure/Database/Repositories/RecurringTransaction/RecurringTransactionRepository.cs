using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

public sealed class RecurringTransactionRepository(
	FinanceTrackerContext context
) : IRecurringTransactionRepository
{
	public async Task<Core.Domains.RecurringTransaction.RecurringTransaction?> GetByIdAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default)
	{
		return await context.RecurringTransactions.AsNoTracking().Where(predicate: r => r.Id == recurringTransactionId)
			.Select(selector: r => Core.Domains.RecurringTransaction.RecurringTransaction.Reconstitute(
				id: r.Id,
				userId: r.UserId,
				accountId: r.AccountId,
				categoryId: r.CategoryId,
				amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
				direction: r.Direction,
				dayOfMonth: r.DayOfMonth,
				description: r.Description,
				isActive: r.IsActive,
				lastExecutedAt: r.LastExecutedAt,
				createdAt: r.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}
}