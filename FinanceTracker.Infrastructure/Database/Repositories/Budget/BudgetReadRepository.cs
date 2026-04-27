using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetReadRepository(
    FinanceTrackerContext context
) : IBudgetReadRepository
{
    public async Task<BudgetDto?> GetByIdAsync(
        Guid budgetId,
        Guid userId,
        CancellationToken ct = default)
    {
        return await context.Budgets.AsNoTracking().Where(predicate: b => b.Id == budgetId && b.UserId == userId)
            .Select(selector: b => new BudgetDto(
                Id: b.Id,
                UserId: b.UserId,
                CategoryId: b.CategoryId,
                Currency: b.Currency,
                Amount: b.Amount,
                From: b.From,
                To: b.To,
                CreatedAt: b.CreatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<BudgetDto?> GetActiveByCategoryAsync(
        Guid userId,
        Guid categoryId,
        DateOnly date,
        CancellationToken ct = default)
    {
        return await context.Budgets.AsNoTracking()
            .Where(predicate: b => b.UserId == userId && b.CategoryId == categoryId && b.From <= date && b.To >= date)
            .Select(selector: b => new BudgetDto(
                Id: b.Id,
                UserId: b.UserId,
                CategoryId: b.CategoryId,
                Currency: b.Currency,
                Amount: b.Amount,
                From: b.From,
                To: b.To,
                CreatedAt: b.CreatedAt
            )).FirstOrDefaultAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<BudgetDto>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await context.Budgets.AsNoTracking().Where(predicate: b => b.UserId == userId)
            .Select(selector: b => new BudgetDto(
                Id: b.Id,
                UserId: b.UserId,
                CategoryId: b.CategoryId,
                Currency: b.Currency,
                Amount: b.Amount,
                From: b.From,
                To: b.To,
                CreatedAt: b.CreatedAt
            )).ToListAsync(cancellationToken: ct);
    }
}