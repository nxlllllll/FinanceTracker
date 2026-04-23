using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class TransactionEntity
{
	public Guid Id { get; init; }
	public Guid AccountId { get; init; }
	public Guid UserId { get; init; }
	public Guid CategoryId { get; set; }
	public decimal Amount { get; init; }
	public DirectionType Direction { get; init; }
	public decimal ExchangeRate { get; init; }
	public bool IsExcluded { get; set; }
	public string? Description { get; set; }
	public bool IsRatePending { get; set; }
	public DateTime OccurredAt { get; init; }
}