using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

public sealed class RecurringTransactionReadRepository(
    FinanceTrackerContext context
) : IRecurringTransactionReadRepository
{
    public async Task<Core.Domains.RecurringTransaction.RecurringTransaction?> GetByIdAsync(
        Guid recurringTransactionId,
        CancellationToken ct = default)
    {
        return await context.RecurringTransactions.AsNoTracking()
            .Where(predicate: r => r.Id == recurringTransactionId)
            .Select(selector: r => Core.Domains.RecurringTransaction.RecurringTransaction.Reconstitute(
                id: r.Id,
                userId: r.UserId,
                accountId: r.AccountId,
                categoryId: r.CategoryId,
                amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
                direction: r.Direction,
                dayOfMonth: r.DayOfMonth,
                description: r.Description,
                isActive: r.IsActive,
                lastExecutedAt: r.LastExecutedAt,
                createdAt: r.CreatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction>> GetByUserIdAsync(
        Guid userId,
        DateTimeOffset? cursorCreatedAt = null,
        Guid? cursorId = null,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        IQueryable<RecurringTransactionEntity> query = context.RecurringTransactions.AsNoTracking()
            .Where(predicate: r => r.UserId == userId);

        if (cursorCreatedAt is not null && cursorId is not null)
			query = query.Where(predicate: r => r.CreatedAt < cursorCreatedAt || r.CreatedAt == cursorCreatedAt && r.Id < cursorId);

        List<Core.Domains.RecurringTransaction.RecurringTransaction> items = await query
            .OrderByDescending(keySelector: r => r.CreatedAt)
            .ThenByDescending(keySelector: r => r.Id)
            .Take(count: pageSize + 1)
            .Select(selector: r => Core.Domains.RecurringTransaction.RecurringTransaction.Reconstitute(
                id: r.Id,
                userId: r.UserId,
                accountId: r.AccountId,
                categoryId: r.CategoryId,
                amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
                direction: r.Direction,
                dayOfMonth: r.DayOfMonth,
                description: r.Description,
                isActive: r.IsActive,
                lastExecutedAt: r.LastExecutedAt,
                createdAt: r.CreatedAt
            )).ToListAsync(cancellationToken: ct);
        
        bool hasNextPage = items.Count > pageSize;
        if (hasNextPage)
            items.RemoveAt(items.Count - 1);

        Core.Domains.RecurringTransaction.RecurringTransaction? last = items.Count > 0 ? items[^1] : null;

        return new PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction>(
            Items: items.AsReadOnly(),
            HasNextPage: hasNextPage,
            NextCursorDate: hasNextPage ? last?.CreatedAt : null,
            NextCursorId: hasNextPage ? last?.Id : null
        );
    }

    public async Task<IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction>> GetDueTodayAsync(
        int dayOfMonth,
        int daysInCurrentMonth,
        DateTimeOffset currentMonthStart,
        CancellationToken ct = default)
    {
        bool isLastDayOfMonth = dayOfMonth == daysInCurrentMonth;
        
        return await context.RecurringTransactions.AsNoTracking()
            .Where(predicate: r => r.IsActive &&
                (r.LastExecutedAt == null || r.LastExecutedAt < currentMonthStart) &&
                (r.DayOfMonth == dayOfMonth || isLastDayOfMonth && r.DayOfMonth > daysInCurrentMonth)
            )
            .Select(selector: r => Core.Domains.RecurringTransaction.RecurringTransaction.Reconstitute(
                id: r.Id,
                userId: r.UserId,
                accountId: r.AccountId,
                categoryId: r.CategoryId,
                amount: Money.Reconstitute(amount: r.Amount, currency: r.Currency),
                direction: r.Direction,
                dayOfMonth: r.DayOfMonth,
                description: r.Description,
                isActive: r.IsActive,
                lastExecutedAt: r.LastExecutedAt,
                createdAt: r.CreatedAt
            )).ToListAsync(cancellationToken: ct);
    }
}
