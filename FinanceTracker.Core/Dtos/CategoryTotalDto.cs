namespace FinanceTracker.Core.Dtos;

public sealed record CategoryTotalDto(
	Guid CategoryId,
	DateOnly Period,
	decimal Total,
	int Count,
	DateTimeOffset UpdatedAt
);
