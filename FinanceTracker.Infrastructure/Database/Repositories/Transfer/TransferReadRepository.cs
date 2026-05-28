using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfer;

public sealed class TransferReadRepository(
    FinanceTrackerContext context
) : ITransferReadRepository
{
    public async Task<Core.Domains.Transfer.Transfer?> GetByIdAsync(
        Guid transferId,
        CancellationToken ct = default)
    {
        return await context.Transfers.AsNoTracking().Where(predicate: t => t.Id == transferId)
            .Select(selector: t => Core.Domains.Transfer.Transfer.Reconstitute(
                id: t.Id,
                userId: t.UserId,
                fromAccountId: t.FromAccountId,
                toAccountId: t.ToAccountId,
                amountFrom: t.AmountFrom,
                currencyFrom: t.CurrencyFrom,
                amountTo: t.AmountTo,
                currencyTo: t.CurrencyTo,
                exchangeRate: t.ExchangeRate,
                isRatePending: t.IsRatePending,
                status: t.Status,
                description: t.Description,
                occurredAt: t.OccurredAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Core.Domains.Transfer.Transfer>> GetAllAsync(
        Guid userId,
        Guid? accountId = null,
        DateTimeOffset? dateFrom = null,
        DateTimeOffset? dateTo = null,
        CancellationToken ct = default)
    {
        IQueryable<TransferEntity> query = context.Transfers.AsNoTracking().Where(predicate: t => t.UserId == userId);

        if (accountId is not null)
            query = query.Where(predicate: t => t.FromAccountId == accountId || t.ToAccountId == accountId);

        if (dateFrom is not null)
            query = query.Where(predicate: t => t.OccurredAt >= dateFrom);

        if (dateTo is not null)
            query = query.Where(predicate: t => t.OccurredAt <= dateTo);

        return await query.OrderByDescending(keySelector: t => t.OccurredAt)
            .Select(selector: t => Core.Domains.Transfer.Transfer.Reconstitute(
               id: t.Id,
               userId: t.UserId,
               fromAccountId: t.FromAccountId,
               toAccountId: t.ToAccountId,
               amountFrom: t.AmountFrom,
               currencyFrom: t.CurrencyFrom,
               amountTo: t.AmountTo,
               currencyTo: t.CurrencyTo,
               exchangeRate: t.ExchangeRate,
               isRatePending: t.IsRatePending,
               status: t.Status,
               description: t.Description,
               occurredAt: t.OccurredAt
            )).ToListAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<PendingRateTransfer>> GetPendingRateAsync(CancellationToken ct = default)
    {
        return await context.Transfers.AsNoTracking().Where(predicate: t => t.IsRatePending)
            .Select(selector: t => new PendingRateTransfer(
                TransferId: t.Id,
                FromAccountId: t.FromAccountId,
                ToAccountId: t.ToAccountId,
                AmountFrom: t.AmountFrom,
                CurrencyFrom: t.CurrencyFrom,
                CurrencyTo: t.CurrencyTo,
                CurrentRate: t.ExchangeRate,
                OccurredAt: t.OccurredAt
            )).ToListAsync(cancellationToken: ct);
    }

    public async Task<int> GetPendingCreditCountAsync(TimeSpan gracePeriod, CancellationToken ct = default)
    {
        DateTimeOffset threshold = DateTimeOffset.UtcNow - gracePeriod;

        return await context.Transfers.AsNoTracking().CountAsync(
            predicate: t => t.Status == Core.Domains.Transfer.TransferStatus.PendingCredit && t.OccurredAt < threshold,
            cancellationToken: ct
        );
    }
}
