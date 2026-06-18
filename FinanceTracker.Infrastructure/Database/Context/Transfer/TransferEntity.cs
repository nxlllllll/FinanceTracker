using FinanceTracker.Core.Domains.Transfer;

namespace FinanceTracker.Infrastructure.Database.Context.Transfer;

public sealed class TransferEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public Guid FromAccountId { get; init; }
	public Guid ToAccountId { get; init; }
	public decimal AmountFrom { get; init; }
	public Core.ValueObjects.Currency CurrencyFrom { get; init; }
	public decimal AmountTo { get; init; }
	public Core.ValueObjects.Currency CurrencyTo { get; init; }
	public decimal ExchangeRate { get; init; }
	public string? Description { get; init; }
	public DateTimeOffset OccurredAt { get; init; }
	public bool IsRatePending { get; init; }
	public TransferStatus Status { get; init; }
	public int RowVersion { get; init; }
}