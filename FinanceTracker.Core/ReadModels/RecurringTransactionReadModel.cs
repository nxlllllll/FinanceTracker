using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record RecurringTransactionReadModel(
	Guid Id,
	Guid UserId,
	Guid AccountId,
	Guid CategoryId,
	Money Amount,
	DirectionType Direction,
	int DayOfMonth,
	string? Description,
	bool IsActive,
	int RowVersion,
	DateTimeOffset? LastExecutedAt,
	DateTimeOffset? LastMissedAt,
	DateTimeOffset CreatedAt
) : IReadModel;
