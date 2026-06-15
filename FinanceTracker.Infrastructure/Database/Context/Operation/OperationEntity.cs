namespace FinanceTracker.Infrastructure.Database.Context.Operation;

public sealed class OperationEntity
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public string Type { get; set; } = null!;
	public DateTimeOffset OccurredAt { get; set; }
	public string? Description { get; set; }
	// Transaction
	public Guid? AccountId { get; set; }
	public Guid? CategoryId { get; set; }
	public decimal? Amount { get; set; }
	public string? CurrencyCode { get; set; }
	public string? DirectionType { get; set; }
	public bool? IsExcluded { get; set; }
	// Transfer
	public Guid? FromAccountId { get; set; }
	public Guid? ToAccountId { get; set; }
	public decimal? AmountFrom { get; set; }
	public string? CurrencyFrom { get; set; }
	public decimal? AmountTo { get; set; }
	public string? CurrencyTo { get; set; }
	public string? Status { get; set; }
}