using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionReadRepository(
	FinanceTrackerContext context
) : ITransactionReadRepository
{
	public async Task<Core.Domains.Transactions.Transaction?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default)
	{
		TransactionEntity? entity = await context.Transactions.AsNoTracking().FirstOrDefaultAsync(
			predicate: transaction => transaction.Id == transactionId,
			cancellationToken: ct
		);

		if (entity is null)
			return null;

		return Core.Domains.Transactions.Transaction.Reconstitute(
			id: entity.Id,
			accountId: entity.AccountId,
			userId: entity.UserId,
			categoryId: entity.CategoryId,
			amount: entity.Amount,
			directionType: entity.Direction,
			exchangeRate: entity.ExchangeRate,
			description: entity.Description,
			isExcluded: entity.IsExcluded,
			occurredAt: entity.OccurredAt
		);
	}
}