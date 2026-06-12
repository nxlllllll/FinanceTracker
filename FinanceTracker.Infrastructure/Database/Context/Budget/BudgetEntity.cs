namespace FinanceTracker.Infrastructure.Database.Context.Budget;

public sealed class BudgetEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid CategoryId { get; init; }
	public DateOnly From { get; init; }
	public DateOnly To { get; init; }
	public Core.ValueObjects.Currency Currency { get; init; }
	public decimal Amount { get; init; }
	public bool IsActive { get; set; }
	public int RowVersion { get; set; }
	public DateTimeOffset CreatedAt { get; init; }
}