namespace FinanceTracker.Infrastructure.Database.Context.Budget;

public sealed class BudgetProgressEntity
{
	public Guid BudgetId { get; init; }
	public decimal Spent { get; init; }
	public int RowVersion { get; init; }
	public DateTimeOffset UpdatedAt { get; init; }
}
