namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class BudgetEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid CategoryId { get; init; }
	public DateOnly From { get; init; }
	public DateOnly To { get; init; }
	public string Currency { get; init; } = null!;
	public decimal Amount { get; init; }
	public DateTime CreatedAt { get; init; }
}