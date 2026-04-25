namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class CategoryTotalEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid CategoryId { get; init; }
	public DateOnly Period { get; init; }
	public decimal Total { get; set; }
	public int TransactionCount { get; set; }
	public DateTime UpdatedAt { get; set; }
}
