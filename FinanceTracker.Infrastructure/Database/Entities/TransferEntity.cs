using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Database.Entities;

public sealed class TransferEntity
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public Guid FromAccountId { get; set; }
	public Guid ToAccountId { get; set; }
	public decimal AmountFrom { get; set; }
	public Currency CurrencyFrom { get; set; }
	public decimal AmountTo { get; set; }
	public Currency CurrencyTo { get; set; }
	public decimal ExchangeRate { get; set; }
	public string? Description { get; set; }
	public DateTime OccurredAt { get; set; }
	public bool IsRatePending { get; set; }
}