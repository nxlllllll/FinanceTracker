using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;

public sealed class RecurringTransactionEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid AccountId { get; init; }
	public Guid CategoryId { get; init; }
	public decimal Amount { get; init; }
	public Core.ValueObjects.Currency Currency { get; init; }
	public DirectionType Direction { get; init; }
	public int DayOfMonth { get; init; }
	public string? Description { get; init; }
	public bool IsActive { get; init; }
	public int RowVersion { get; init; }
	public DateTimeOffset? LastExecutedAt { get; init; }
	public DateTimeOffset CreatedAt { get; init; }
}