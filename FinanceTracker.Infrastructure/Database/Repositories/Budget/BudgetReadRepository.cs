using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetReadRepository(
    FinanceTrackerContext context
) : IBudgetReadRepository
{
    public async Task<Core.Domains.Budget.Budget?> GetByIdAsync(
        Guid budgetId,
        Guid userId,
        CancellationToken ct = default)
    {
        return await context.Budgets.AsNoTracking().Where(predicate: b => b.Id == budgetId && b.UserId == userId)
            .Select(selector: b => Core.Domains.Budget.Budget.Reconstitute(
                id: b.Id,
                userId: b.UserId,
                categoryId: b.CategoryId,
                amount: Money.Reconstitute(amount: b.Amount, currency: b.Currency),
                from: b.From,
                to: b.To,
                createdAt: b.CreatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<Core.Domains.Budget.Budget?> GetActiveByCategoryAsync(
        Guid userId,
        Guid categoryId,
        DateOnly date,
        CancellationToken ct = default)
    {
        return await context.Budgets.AsNoTracking()
            .Where(predicate: b => b.UserId == userId && b.CategoryId == categoryId && b.From <= date && b.To >= date)
            .Select(selector: b => Core.Domains.Budget.Budget.Reconstitute(
                id: b.Id,
                userId: b.UserId,
                categoryId: b.CategoryId,
                amount: Money.Reconstitute(amount: b.Amount, currency: b.Currency),
                from: b.From,
                to: b.To,
                createdAt: b.CreatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Core.Domains.Budget.Budget>> GetAllAsync(
        Guid userId,
        DateTime? cursorCreatedAt = null,
        Guid? cursorId = null,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        IQueryable<BudgetEntity> budgets = context.Budgets.AsNoTracking().Where(predicate: b => b.UserId == userId);

        if (cursorCreatedAt is not null && cursorId is not null)
            budgets = budgets.Where(predicate: b => b.CreatedAt < cursorCreatedAt || b.CreatedAt == cursorCreatedAt && b.Id < cursorId);

        return await budgets
            .OrderByDescending(keySelector: b => b.CreatedAt)
            .ThenByDescending(keySelector: b => b.Id)
            .Take(count: pageSize)
            .Select(selector: b => Core.Domains.Budget.Budget.Reconstitute(
                id: b.Id,
                userId: b.UserId,
                categoryId: b.CategoryId,
                amount: Money.Reconstitute(amount: b.Amount, currency: b.Currency),
                from: b.From,
                to: b.To,
                createdAt: b.CreatedAt
            )).ToListAsync(cancellationToken: ct);
    }
}