using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;

public sealed class RecurringTransactionEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid AccountId { get; init; }
	public Guid CategoryId { get; init; }
	public decimal Amount { get; set; }
	public Core.ValueObjects.Currency Currency { get; set; }
	public DirectionType Direction { get; init; }
	public int DayOfMonth { get; set; }
	public string? Description { get; init; }
	public bool IsActive { get; set; }
	public DateTimeOffset? LastExecutedAt { get; set; }
	public DateTimeOffset CreatedAt { get; init; }
}
