using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionReadRepository(
	FinanceTrackerContext context
) : ITransactionReadRepository
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
                occurredAt: t.OccurredAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

	public async Task<PagedResult<Core.Domains.Transaction.Transaction>> GetAllAsync(
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
            query = query.Where(predicate: t => t.CategoryId == categoryId);

        if (direction is not null)
            query = query.Where(predicate: t => t.Direction == direction);

        if (isExcluded is not null)
            query = query.Where(predicate: t => t.IsExcluded == isExcluded);

        if (dateFrom is not null)
            query = query.Where(predicate: t => t.OccurredAt >= dateFrom);

        if (dateTo is not null)
            query = query.Where(predicate: t => t.OccurredAt <= dateTo);

        if (cursorOccurredAt is not null && cursorId is not null)
            query = query.Where(predicate: t => t.OccurredAt < cursorOccurredAt || t.OccurredAt == cursorOccurredAt && t.Id < cursorId);
        
        List<Core.Domains.Transaction.Transaction> items = await query.OrderByDescending(keySelector: t => t.OccurredAt)
		.ThenByDescending(keySelector: t => t.Id)
		.Take(count: pageSize + 1)
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
			occurredAt: t.OccurredAt
		)).ToListAsync(cancellationToken: ct);
		
		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(items.Count - 1);

		Core.Domains.Transaction.Transaction? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<Core.Domains.Transaction.Transaction>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.OccurredAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
    }

    public async Task<IReadOnlyList<PendingRateTransaction>> GetPendingRateAsync(CancellationToken ct = default)
    {
        return await context.Transactions.AsNoTracking().Where(predicate: t => t.IsRatePending).Join(
            inner: context.Users,
            outerKeySelector: t => t.UserId,
            innerKeySelector: u => u.Id,
            resultSelector: (t, u) => new PendingRateTransaction(
                TransactionId: t.Id,
                AccountId: t.AccountId,
                Amount: t.Amount,
                TransactionCurrency: t.Currency,
                BaseCurrency: u.BaseCurrencyCode,
                CurrentRate: t.ExchangeRate,
                Direction: t.Direction,
                OccurredAt: t.OccurredAt
            )
        ).ToListAsync(cancellationToken: ct);
    }
}
