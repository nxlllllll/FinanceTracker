using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetReadRepository
{
    Task<BudgetDto?> GetByIdAsync(
        Guid budgetId,
        Guid userId,
        CancellationToken ct = default
    );

    Task<BudgetDto?> GetActiveByCategoryAsync(
        Guid userId,
        Guid categoryId,
        DateOnly date,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<BudgetDto>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default
    );
}