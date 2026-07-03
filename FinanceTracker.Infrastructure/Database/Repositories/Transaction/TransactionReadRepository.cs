using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionReadRepository(FinanceTrackerContext context) : ITransactionReadRepository
{
	public async Task<TransactionReadModel?> GetByIdAsync(
		Guid transactionId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Transactions.AsNoTracking().Where(predicate: t => t.Id == transactionId && t.UserId == userId)
			.Select(selector: t => new TransactionReadModel(
				Id: t.Id,
				AccountId: t.AccountId,
				UserId: t.UserId,
				CategoryId: t.CategoryId,
				Amount: Money.Reconstitute(amount: t.Amount, currency: t.Currency),
				Direction: t.Direction,
				ExchangeRate: t.ExchangeRate,
				IsExcluded: t.IsExcluded,
				IsRatePending: t.IsRatePending,
				Description: t.Description,
				OccurredAt: t.OccurredAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<PagedResult<TransactionReadModel>> GetAllAsync(
		Guid userId,
		Guid accountId,
		Guid? categoryId = null,
		DirectionType? direction = null,
		bool? isExcluded = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<Context.Transaction.TransactionEntity> query = context.Transactions.AsNoTracking()
			.Where(predicate: t => t.AccountId == accountId && t.UserId == userId);

		if (categoryId is not null)
			query = query.Where(predicate: t => t.CategoryId == categoryId.Value);

		if (direction is not null)
			query = query.Where(predicate: t => t.Direction == direction.Value);

		if (isExcluded is not null)
			query = query.Where(predicate: t => t.IsExcluded == isExcluded.Value);

		if (dateFrom is not null)
			query = query.Where(predicate: t => t.OccurredAt >= dateFrom.Value);

		if (dateTo is not null)
			query = query.Where(predicate: t => t.OccurredAt <= dateTo.Value);

		if (cursorOccurredAt is not null && cursorId is not null)
			query = query.Where(predicate: t =>
				t.OccurredAt < cursorOccurredAt.Value ||
				(t.OccurredAt == cursorOccurredAt.Value && t.Id < cursorId.Value));

		List<TransactionReadModel> items = await query.OrderByDescending(keySelector: t => t.OccurredAt)
			.ThenByDescending(keySelector: t => t.Id)
			.Take(count: pageSize + 1)
			.Select(selector: t => new TransactionReadModel(
				Id: t.Id,
				AccountId: t.AccountId,
				UserId: t.UserId,
				CategoryId: t.CategoryId,
				Amount: Money.Reconstitute(amount: t.Amount, currency: t.Currency),
				Direction: t.Direction,
				ExchangeRate: t.ExchangeRate,
				IsExcluded: t.IsExcluded,
				IsRatePending: t.IsRatePending,
				Description: t.Description,
				OccurredAt: t.OccurredAt
			)).ToListAsync(cancellationToken: ct);

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(index: items.Count - 1);

		TransactionReadModel? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<TransactionReadModel>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.OccurredAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}

	public async Task<IReadOnlyList<PendingRateTransaction>> GetPendingRateAsync(CancellationToken ct = default)
	{
		return await context.Transactions.AsNoTracking().Where(predicate: t => t.IsRatePending).Select(selector: t => new PendingRateTransaction(
			TransactionId: t.Id,
			AccountId: t.AccountId,
			Amount: t.Amount,
			TransactionCurrency: t.Currency,
			BaseCurrency: t.BaseCurrency,
			CurrentRate: t.ExchangeRate,
			Direction: t.Direction,
			RowVersion: t.RowVersion,
			OccurredAt: t.OccurredAt
		)).ToListAsync(cancellationToken: ct);
	}
}
