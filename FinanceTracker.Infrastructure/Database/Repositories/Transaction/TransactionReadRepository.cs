using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionReadRepository(
	FinanceTrackerContext context
) : ITransactionReadRepository
{
	public async Task<Core.Domains.Transaction.Transaction?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default)
	{
		TransactionEntity? entity = await context.Transactions.AsNoTracking().FirstOrDefaultAsync(
			predicate: transaction => transaction.Id == transactionId,
			cancellationToken: ct
		);

		if (entity is null)
			return null;

		return Core.Domains.Transaction.Transaction.Reconstitute(
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

	public async Task<IReadOnlyList<Core.Domains.Transaction.Transaction>> GetAllAsync(
		Guid accountId,
		Guid? categoryId = null,
		DirectionType? direction = null,
		bool? isExcluded = null,
		DateTime? dateFrom = null,
		DateTime? dateTo = null,
		CancellationToken ct = default)
	{
		IQueryable<TransactionEntity> query = context.Transactions.AsNoTracking().Where(predicate: t => t.AccountId == accountId);

		if (categoryId is not null)
			query = query.Where(predicate: t => t.CategoryId == categoryId);

		if (direction is not null)
			query = query.Where(predicate: t => t.Direction == direction);

		if (isExcluded is not null)
			query = query.Where(predicate: t => t.IsExcluded == isExcluded);

		if (dateFrom is not null)
			query = query.Where(predicate: t => t.OccurredAt >= dateFrom);

		if (dateTo is not null)
			query = query.Where(predicate: t => t.OccurredAt <= dateTo);

		return await query.OrderByDescending(keySelector: t => t.OccurredAt)
			.Select(selector: t => Core.Domains.Transaction.Transaction.Reconstitute(
				id: t.Id,
				accountId: t.AccountId,
				userId: t.UserId,
				categoryId: t.CategoryId,
				amount: t.Amount,
				directionType: t.Direction,
				exchangeRate: t.ExchangeRate,
				description: t.Description,
				isExcluded: t.IsExcluded,
				occurredAt: t.OccurredAt
			)).ToListAsync(cancellationToken: ct);
	}
}