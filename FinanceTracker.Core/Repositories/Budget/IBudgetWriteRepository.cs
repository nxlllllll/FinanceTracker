namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetWriteRepository
{
    Task CreateAsync(
        Guid budgetId,
        Guid userId,
        Guid categoryId,
        string currency,
        decimal amount,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default
    );

    Task ChangeAmountAsync(
        Guid budgetId,
        decimal amount,
        CancellationToken ct = default
    );

    Task ChangePeriodAsync(
        Guid budgetId,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken ct = default
    );

    Task DeleteAsync(
        Guid budgetId,
        CancellationToken ct = default
    );
}