using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionRepository(
	FinanceTrackerContext context
) : ITransactionRepository
{
	public async Task<Core.Domains.Transaction.Transaction?> GetByIdAsync(
		Guid transactionId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Transactions.AsNoTracking().Where(predicate: t => t.Id == transactionId && t.UserId == userId)
			.Select(selector: t => Core.Domains.Transaction.Transaction.Reconstitute(
				id: t.Id,
				accountId: t.AccountId,
				userId: t.UserId,
				categoryId: t.CategoryId,
				amount: Money.Reconstitute(amount: t.Amount, currency: t.Currency),
				direction: t.Direction,
				exchangeRate: t.ExchangeRate,
				isExcluded: t.IsExcluded,
				isRatePending: t.IsRatePending,
				description: t.Description,
				rowVersion: t.RowVersion,
				occurredAt: t.OccurredAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}
}