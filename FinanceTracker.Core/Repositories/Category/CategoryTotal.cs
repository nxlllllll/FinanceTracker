namespace FinanceTracker.Core.Repositories.Category;

public sealed record CategoryTotal(
	Guid CategoryId,
	DateOnly Period,
	decimal Total,
	int Count,
	DateTimeOffset UpdatedAt
);
