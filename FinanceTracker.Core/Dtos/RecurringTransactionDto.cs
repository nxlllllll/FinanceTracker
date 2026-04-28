using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Dtos;

public sealed record RecurringTransactionDto(
	Guid Id,
	Guid UserId,
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	int DayOfMonth,
	string? Description,
	bool IsActive,
	DateTime? LastExecutedAt,
	DateTime CreatedAt
);