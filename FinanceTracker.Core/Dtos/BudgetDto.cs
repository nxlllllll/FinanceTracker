namespace FinanceTracker.Core.Dtos;

public sealed record BudgetDto(
	Guid Id,
	Guid UserId,
	Guid CategoryId,
	string Currency,
	decimal Amount,
	DateOnly From,
	DateOnly To,
	DateTime CreatedAt
);