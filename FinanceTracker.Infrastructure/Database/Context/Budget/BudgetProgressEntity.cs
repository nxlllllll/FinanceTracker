namespace FinanceTracker.Infrastructure.Database.Context.Budget;

public sealed class BudgetProgressEntity
{
	public Guid BudgetId { get; init; }
	public decimal Spent { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
}
