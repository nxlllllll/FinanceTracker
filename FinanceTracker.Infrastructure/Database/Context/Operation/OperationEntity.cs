namespace FinanceTracker.Infrastructure.Database.Context.Operation;

public sealed class OperationEntity
{
	public Guid Id { get; init; }
	public Guid UserId { get; init; }
	public string Type { get; init; } = null!;
	public DateTimeOffset OccurredAt { get; init; }
	public string? Description { get; init; }
	// Transaction
	public Guid? AccountId { get; init; }
	public Guid? CategoryId { get; init; }
	public decimal? Amount { get; init; }
	public string? CurrencyCode { get; init; }
	public string? DirectionType { get; init; }
	public bool? IsExcluded { get; init; }
	// Transfer
	public Guid? FromAccountId { get; init; }
	public Guid? ToAccountId { get; init; }
	public decimal? AmountFrom { get; init; }
	public string? CurrencyFrom { get; init; }
	public decimal? AmountTo { get; init; }
	public string? CurrencyTo { get; init; }
	public string? Status { get; init; }
}