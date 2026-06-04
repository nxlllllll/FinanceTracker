using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record BudgetReadModel(
	Guid Id,
	Guid UserId,
	Guid CategoryId,
	Money Amount,
	DateOnly From,
	DateOnly To,
	bool IsActive,
	DateTimeOffset CreatedAt
) : IReadModel;