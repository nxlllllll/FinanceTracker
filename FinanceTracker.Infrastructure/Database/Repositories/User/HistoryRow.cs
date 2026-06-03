namespace FinanceTracker.Infrastructure.Database.Repositories.User;

internal sealed class HistoryRow
{
	public Guid Id { get; init; }
	public string Type { get; init; } = null!;
	public string? Description { get; init; }
	public DateTimeOffset OccurredAt { get; init; }

	// Transaction-only
	public Guid? AccountId { get; init; }
	public Guid? CategoryId { get; init; }
	public decimal? Amount { get; init; }
	public string? CurrencyCode { get; init; }
	public string? Direction { get; init; }
	public bool? IsExcluded { get; init; }

	// Transfer-only
	public Guid? FromAccountId { get; init; }
	public Guid? ToAccountId { get; init; }
	public decimal? AmountFrom { get; init; }
	public string? CurrencyFrom { get; init; }
	public decimal? AmountTo { get; init; }
	public string? CurrencyTo { get; init; }
}