using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfers;

public sealed class TransferReadRepository(
    FinanceTrackerContext context
) : ITransferReadRepository
{
    public async Task<Transfer?> GetByIdAsync(
        Guid transferId,
        CancellationToken ct = default)
    {
        return await context.Transfers.AsNoTracking().Where(predicate: t => t.Id == transferId)
            .Select(selector: t => Transfer.Reconstitute(
                id: t.Id,
                userId: t.UserId,
                fromAccountId: t.FromAccountId,
                toAccountId: t.ToAccountId,
                amountFrom: t.AmountFrom,
                currencyFrom: t.CurrencyFrom,
                amountTo: t.AmountTo,
                currencyTo: t.CurrencyTo,
                exchangeRate: t.ExchangeRate,
                isExcluded: t.IsExcluded,
                isRatePending: t.IsRatePending,
                description: t.Description,
                occurredAt: t.OccurredAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Transfer>> GetAllAsync(
        Guid userId,
        Guid? accountId = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        IQueryable<TransferEntity> query = context.Transfers.AsNoTracking().Where(predicate: t => t.UserId == userId);

        if (accountId is not null)
            query = query.Where(predicate: t => t.FromAccountId == accountId || t.ToAccountId == accountId);

        if (dateFrom is not null)
            query = query.Where(predicate: t => t.OccurredAt >= dateFrom);

        if (dateTo is not null)
            query = query.Where(predicate: t => t.OccurredAt <= dateTo);

        return await query
            .OrderByDescending(keySelector: t => t.OccurredAt)
            .Select(selector: t => Transfer.Reconstitute(
                id: t.Id,
                userId: t.UserId,
                fromAccountId: t.FromAccountId,
                toAccountId: t.ToAccountId,
                amountFrom: t.AmountFrom,
                currencyFrom: t.CurrencyFrom,
                amountTo: t.AmountTo,
                currencyTo: t.CurrencyTo,
                exchangeRate: t.ExchangeRate,
                isExcluded: t.IsExcluded,
                isRatePending: t.IsRatePending,
                description: t.Description,
                occurredAt: t.OccurredAt
            )).ToListAsync(cancellationToken: ct);
    }
}