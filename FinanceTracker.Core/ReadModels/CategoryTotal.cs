namespace FinanceTracker.Core.ReadModels;

public sealed record CategoryTotal(
	Guid CategoryId,
	DateOnly Period,
	decimal Total,
	int Count,
	DateTimeOffset? UpdatedAt
) : IReadModel;
