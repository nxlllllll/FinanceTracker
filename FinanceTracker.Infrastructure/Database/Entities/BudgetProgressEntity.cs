namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class BudgetProgressEntity
{
	public Guid BudgetId { get; init; }
	public decimal Spent { get; set; }
	public DateTime UpdatedAt { get; set; }
}