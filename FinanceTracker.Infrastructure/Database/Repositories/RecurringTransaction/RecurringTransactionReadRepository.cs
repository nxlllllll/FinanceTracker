using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.ValueObjects;
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
                amount: new Money(amount: r.Amount, currency: r.Currency),
                direction: r.Direction,
                dayOfMonth: r.DayOfMonth,
                description: r.Description,
                isActive: r.IsActive,
                lastExecutedAt: r.LastExecutedAt,
                createdAt: r.CreatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction>> GetByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await context.RecurringTransactions.AsNoTracking()
            .Where(predicate: r => r.UserId == userId)
            .OrderBy(keySelector: r => r.CreatedAt)
            .Select(selector: r => Core.Domains.RecurringTransaction.RecurringTransaction.Reconstitute(
                id: r.Id,
                userId: r.UserId,
                accountId: r.AccountId,
                categoryId: r.CategoryId,
                amount: new Money(amount: r.Amount, currency: r.Currency),
                direction: r.Direction,
                dayOfMonth: r.DayOfMonth,
                description: r.Description,
                isActive: r.IsActive,
                lastExecutedAt: r.LastExecutedAt,
                createdAt: r.CreatedAt
            )).ToListAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction>> GetDueTodayAsync(
        int dayOfMonth,
        int daysInCurrentMonth,
        DateTime currentMonthStart,
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
                amount: new Money(amount: r.Amount, currency: r.Currency),
                direction: r.Direction,
                dayOfMonth: r.DayOfMonth,
                description: r.Description,
                isActive: r.IsActive,
                lastExecutedAt: r.LastExecutedAt,
                createdAt: r.CreatedAt
            )).ToListAsync(cancellationToken: ct);
    }
}