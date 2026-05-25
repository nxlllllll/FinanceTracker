using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class BudgetEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid CategoryId { get; init; }
	public DateOnly From { get; init; }
	public DateOnly To { get; init; }
	public Currency Currency { get; init; }
	public decimal Amount { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
}
