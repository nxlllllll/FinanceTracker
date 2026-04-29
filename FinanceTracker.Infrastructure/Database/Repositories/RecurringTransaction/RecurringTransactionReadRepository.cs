using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

public sealed class RecurringTransactionReadRepository(
    FinanceTrackerContext context
) : IRecurringTransactionReadRepository
{
    public async Task<RecurringTransactionDto?> GetByIdAsync(
        Guid recurringTransactionId,
        CancellationToken ct = default)
    {
        return await context.RecurringTransactions.AsNoTracking()
            .Where(predicate: r => r.Id == recurringTransactionId)
            .Select(selector: r => new RecurringTransactionDto(
                Id: r.Id,
                UserId: r.UserId,
                AccountId: r.AccountId,
                CategoryId: r.CategoryId,
                Amount: r.Amount,
                Currency: r.Currency,
                Direction: r.Direction,
                DayOfMonth: r.DayOfMonth,
                Description: r.Description,
                IsActive: r.IsActive,
                LastExecutedAt: r.LastExecutedAt,
                CreatedAt: r.CreatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<RecurringTransactionDto>> GetByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await context.RecurringTransactions.AsNoTracking()
            .Where(predicate: r => r.UserId == userId)
            .OrderBy(keySelector: r => r.CreatedAt)
            .Select(selector: r => new RecurringTransactionDto(
                Id: r.Id,
                UserId: r.UserId,
                AccountId: r.AccountId,
                CategoryId: r.CategoryId,
                Amount: r.Amount,
                Currency: r.Currency,
                Direction: r.Direction,
                DayOfMonth: r.DayOfMonth,
                Description: r.Description,
                IsActive: r.IsActive,
                LastExecutedAt: r.LastExecutedAt,
                CreatedAt: r.CreatedAt
            )).ToListAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<RecurringTransactionDto>> GetDueTodayAsync(
        int dayOfMonth,
        int daysInCurrentMonth,
        DateTime currentMonthStart,
        CancellationToken ct = default)
    {
        bool isLastDayOfMonth = dayOfMonth == daysInCurrentMonth;
        
        return await context.RecurringTransactions.AsNoTracking()
            .Where(predicate: r => r.IsActive &&
                (r.LastExecutedAt == null || r.LastExecutedAt < currentMonthStart) &&
                (r.DayOfMonth == dayOfMonth || (isLastDayOfMonth && r.DayOfMonth > daysInCurrentMonth))
            )
            .Select(selector: r => new RecurringTransactionDto(
                Id: r.Id,
                UserId: r.UserId,
                AccountId: r.AccountId,
                CategoryId: r.CategoryId,
                Amount: r.Amount,
                Currency: r.Currency,
                Direction: r.Direction,
                DayOfMonth: r.DayOfMonth,
                Description: r.Description,
                IsActive: r.IsActive,
                LastExecutedAt: r.LastExecutedAt,
                CreatedAt: r.CreatedAt
            )).ToListAsync(cancellationToken: ct);
    }
}