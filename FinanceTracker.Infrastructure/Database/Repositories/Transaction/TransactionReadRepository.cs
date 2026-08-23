using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
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
				RateStatus: t.RateStatus,
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
		IQueryable<TransactionEntity> query = context.Transactions.AsNoTracking()
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
				RateStatus: t.RateStatus,
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

	public async Task<IReadOnlyList<PendingRateTransaction>> GetPendingRateAsync(
		int batchSize,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		CancellationToken ct = default)
	{
		IQueryable<TransactionEntity> query = context.Transactions.AsNoTracking()
												.Where(predicate: t => t.RateStatus == RateStatus.Pending);

		if (cursorOccurredAt is not null && cursorId is not null)
		{
			query = query.Where(predicate: t =>
				t.OccurredAt > cursorOccurredAt.Value ||
				(t.OccurredAt == cursorOccurredAt.Value && t.Id > cursorId.Value)
			);
		}

		return await query.OrderBy(keySelector: t => t.OccurredAt)
			.ThenBy(keySelector: t => t.Id)
			.Take(count: batchSize)
			.Select(selector: t => new PendingRateTransaction(
				TransactionId: t.Id,
				UserId: t.UserId,
				TransactionCurrency: t.Currency,
				BaseCurrency: t.BaseCurrency,
				OccurredAt: t.OccurredAt,
				RateStatusChangedAt: t.RateStatusChangedAt
			)).ToListAsync(cancellationToken: ct);
	}

	public async Task<bool> HasPendingRateAsync(Guid accountId, CancellationToken ct = default)
	{
		return await context.Transactions.AsNoTracking().AnyAsync(
			predicate: t => t.AccountId == accountId && t.RateStatus == RateStatus.Pending,
			cancellationToken: ct
		);
	}
}
