using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;
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
                currency: b.Currency,
                amount: b.Amount,
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
                currency: b.Currency,
                amount: b.Amount,
                from: b.From,
                to: b.To,
                createdAt: b.CreatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Core.Domains.Budget.Budget>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await context.Budgets.AsNoTracking().Where(predicate: b => b.UserId == userId)
            .Select(selector: b => Core.Domains.Budget.Budget.Reconstitute(
                id: b.Id,
                userId: b.UserId,
                categoryId: b.CategoryId,
                currency: b.Currency,
                amount: b.Amount,
                from: b.From,
                to: b.To,
                createdAt: b.CreatedAt
            )).ToListAsync(cancellationToken: ct);
    }
}