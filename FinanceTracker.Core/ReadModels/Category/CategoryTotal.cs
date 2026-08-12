namespace FinanceTracker.Core.ReadModels.Category;

public sealed record CategoryTotal(
	Guid CategoryId,
	DateOnly Period,
	decimal Total,
	int Count,
	DateTimeOffset? UpdatedAt
) : IReadModel;
