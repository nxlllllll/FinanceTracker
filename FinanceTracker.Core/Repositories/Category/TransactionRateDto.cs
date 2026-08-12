namespace FinanceTracker.Core.Repositories.Category;

/// <summary>
/// One transaction paired with the exchange rate that applied on
/// the date it occurred, as read while rebuilding category totals.
/// </summary>
public sealed record TransactionRateDto(
	Guid Id,
	Guid CategoryId,
	DateOnly Period,
	decimal Amount,
	string CurrencyCode,
	decimal? Rate
);
