using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Infrastructure.Database.Context.Transaction;

public sealed class TransactionEntity
{
	public Guid Id { get; init; }
	public Guid AccountId { get; init; }
	public Guid UserId { get; init; }
	public Guid CategoryId { get; set; }
	public decimal Amount { get; init; }
	public Core.ValueObjects.Currency Currency { get; init; }
	public DirectionType Direction { get; init; }
	public decimal ExchangeRate { get; init; }
	public bool IsExcluded { get; set; }
	public string? Description { get; set; }
	public bool IsRatePending { get; set; }
	public int RowVersion { get; set; }
	public DateTimeOffset OccurredAt { get; init; }
}