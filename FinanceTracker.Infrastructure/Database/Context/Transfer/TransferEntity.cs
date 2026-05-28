using FinanceTracker.Core.Domains.Transfer;

namespace FinanceTracker.Infrastructure.Database.Context.Transfer;

public sealed class TransferEntity
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public Guid FromAccountId { get; set; }
	public Guid ToAccountId { get; set; }
	public decimal AmountFrom { get; set; }
	public Core.ValueObjects.Currency CurrencyFrom { get; set; }
	public decimal AmountTo { get; set; }
	public Core.ValueObjects.Currency CurrencyTo { get; set; }
	public decimal ExchangeRate { get; set; }
	public string? Description { get; set; }
	public DateTimeOffset OccurredAt { get; set; }
	public bool IsRatePending { get; set; }
	public TransferStatus Status { get; set; }
}