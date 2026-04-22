using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionReadRepository(
	FinanceTrackerContext context
) : ITransactionReadRepository
{
	public async Task<TransactionDto?> GetByIdAsync(
        Guid transactionId,
        CancellationToken ct = default)
    {
        return await context.Transactions.AsNoTracking().Where(predicate: t => t.Id == transactionId)
            .Select(selector: t => new TransactionDto(
                Id: t.Id,
                AccountId: t.AccountId,
                UserId: t.UserId,
                CategoryId: t.CategoryId,
                Amount: t.Amount,
                Direction: t.Direction,
                ExchangeRate: t.ExchangeRate,
                IsExcluded: t.IsExcluded,
                Description: t.Description,
                OccurredAt: t.OccurredAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TransactionDto>> GetAllAsync(
        Guid accountId,
        Guid? categoryId = null,
        DirectionType? direction = null,
        bool? isExcluded = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
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

        return await query.OrderByDescending(keySelector: t => t.OccurredAt)
            .Select(selector: t => new TransactionDto(
                Id: t.Id,
                AccountId: t.AccountId,
                UserId: t.UserId,
                CategoryId: t.CategoryId,
                Amount: t.Amount,
                Direction: t.Direction,
                ExchangeRate: t.ExchangeRate,
                IsExcluded: t.IsExcluded,
                Description: t.Description,
                OccurredAt: t.OccurredAt
            )).ToListAsync(cancellationToken: ct);
    }

    public async Task<bool> ExistsAsync(
        Guid transactionId,
        CancellationToken ct = default)
    {
        return await context.Transactions.AsNoTracking().AnyAsync(
            predicate: t => t.Id == transactionId,
            cancellationToken: ct
        );
    }
}