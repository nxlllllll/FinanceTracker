using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transfer;

public sealed class TransferReadRepository(
    FinanceTrackerContext context
) : ITransferReadRepository
{
    public async Task<TransferReadModel?> GetByIdAsync(
        Guid transferId,
        CancellationToken ct = default)
    {
        return await context.Transfers.AsNoTracking().Where(predicate: t => t.Id == transferId)
            .Select(selector: t => new TransferReadModel(
                Id: t.Id,
                UserId: t.UserId,
                FromAccountId: t.FromAccountId,
                ToAccountId: t.ToAccountId,
                AmountFrom: Money.Reconstitute(amount: t.AmountFrom, currency: t.CurrencyFrom),
                AmountTo: Money.Reconstitute(amount: t.AmountTo, currency: t.CurrencyTo),
                ExchangeRate: t.ExchangeRate,
                IsRatePending: t.IsRatePending,
                Status: t.Status,
                Description: t.Description,
                OccurredAt: t.OccurredAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TransferReadModel>> GetAllAsync(
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
            .Select(selector: t => new TransferReadModel(
                Id: t.Id,
                UserId: t.UserId,
                FromAccountId: t.FromAccountId,
                ToAccountId: t.ToAccountId,
                AmountFrom: Money.Reconstitute(amount: t.AmountFrom, currency: t.CurrencyFrom),
                AmountTo: Money.Reconstitute(amount: t.AmountTo, currency: t.CurrencyTo),
                ExchangeRate: t.ExchangeRate,
                IsRatePending: t.IsRatePending,
                Status: t.Status,
                Description: t.Description,
                OccurredAt: t.OccurredAt
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
