using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Transaction;
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
        CancellationToken ct = default)
    {
        return await context.Transactions.AsNoTracking().Where(predicate: t => t.Id == transactionId)
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

    public async Task<IReadOnlyList<Core.Domains.Transaction.Transaction>> GetAllAsync(
        Guid accountId,
        Guid? categoryId = null,
        DirectionType? direction = null,
        bool? isExcluded = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        DateTime? cursorOccurredAt = null,
        Guid? cursorId = null,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        IQueryable<TransactionEntity> query = context.Transactions.AsNoTracking()
            .Where(predicate: t => t.AccountId == accountId);

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
        
        return await query.OrderByDescending(keySelector: t => t.OccurredAt)
            .ThenByDescending(keySelector: t => t.Id)
            .Take(count: pageSize)
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
    }

    public async Task<bool> ExistsAsync(
        Guid userId,
        Guid transactionId,
        CancellationToken ct = default)
    {
        return await context.Transactions.AsNoTracking().AnyAsync(
            predicate: t => t.Id == transactionId && t.UserId == userId,
            cancellationToken: ct
        );
    }
}