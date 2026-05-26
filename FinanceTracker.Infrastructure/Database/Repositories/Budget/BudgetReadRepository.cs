using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Budget;
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

    public async Task<bool> HasOverlappingAsync(
        Guid userId,
        Guid categoryId,
        DateOnly from,
        DateOnly to,
        Guid? excludeBudgetId = null,
        CancellationToken ct = default)
    {
        IQueryable<BudgetEntity> query = context.Budgets.AsNoTracking().Where(
            predicate: b => b.UserId == userId && b.CategoryId == categoryId && b.From < to && b.To > from
        );

        if (excludeBudgetId is not null)
            query = query.Where(predicate: b => b.Id != excludeBudgetId);

        return await query.AnyAsync(cancellationToken: ct);
    }
    
	public async Task<PagedResult<Core.Domains.Budget.Budget>> GetAllAsync(
        Guid userId,
        DateTimeOffset? cursorCreatedAt = null,
        Guid? cursorId = null,
        int pageSize = 20,
        CancellationToken ct = default)
    {
		IQueryable<BudgetEntity> query = context.Budgets.AsNoTracking().Where(predicate: b => b.UserId == userId);

        if (cursorCreatedAt is not null && cursorId is not null)
			query = query.Where(predicate: b => b.CreatedAt < cursorCreatedAt || b.CreatedAt == cursorCreatedAt && b.Id < cursorId);

        List<Core.Domains.Budget.Budget> items = await query
            .OrderByDescending(keySelector: b => b.CreatedAt)
            .ThenByDescending(keySelector: b => b.Id)
            .Take(count: pageSize + 1)
            .Select(selector: b => Core.Domains.Budget.Budget.Reconstitute(
                id: b.Id,
                userId: b.UserId,
                categoryId: b.CategoryId,
                amount: Money.Reconstitute(amount: b.Amount, currency: b.Currency),
                from: b.From,
                to: b.To,
                createdAt: b.CreatedAt
            )).ToListAsync(cancellationToken: ct);
        
        bool hasNextPage = items.Count > pageSize;
        if (hasNextPage)
            items.RemoveAt(items.Count - 1);

        Core.Domains.Budget.Budget? last = items.Count > 0 ? items[^1] : null;

        return new PagedResult<Core.Domains.Budget.Budget>(
            Items: items.AsReadOnly(),
            HasNextPage: hasNextPage,
            NextCursorDate: hasNextPage ? last?.CreatedAt : null,
            NextCursorId: hasNextPage ? last?.Id : null
        );
    }
}
