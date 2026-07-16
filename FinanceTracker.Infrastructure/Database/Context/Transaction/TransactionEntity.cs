using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Infrastructure.Database.Context.Transaction;

public sealed class TransactionEntity
{
	public Guid Id { get; init; }
	public Guid AccountId { get; init; }
	public Guid UserId { get; init; }
	public Guid CategoryId { get; init; }
	public decimal Amount { get; init; }
	public Core.ValueObjects.Currency Currency { get; init; }
	public Core.ValueObjects.Currency BaseCurrency { get; init; }
	public DirectionType Direction { get; init; }
	public decimal ExchangeRate { get; init; }
	public RateStatus RateStatus { get; init; }
	public DateTimeOffset RateStatusChangedAt { get; init; }
	public bool IsExcluded { get; init; }
	public string? Description { get; init; }
	public int RowVersion { get; init; }
	public DateTimeOffset OccurredAt { get; init; }
}
