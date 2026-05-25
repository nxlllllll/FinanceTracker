using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class RecurringTransactionEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid AccountId { get; init; }
	public Guid CategoryId { get; init; }
	public decimal Amount { get; set; }
	public Currency Currency { get; set; }
	public DirectionType Direction { get; init; }
	public int DayOfMonth { get; set; }
	public string? Description { get; init; }
	public bool IsActive { get; set; }
	public DateTimeOffset? LastExecutedAt { get; set; }
	public DateTimeOffset CreatedAt { get; init; }
}
