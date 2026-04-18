namespace FinanceTracker.Core.Dtos;

public sealed record AccountDto(
	Guid Id,
	Guid UserId,
	string Name,
	string AccountType,
	string Currency,
	decimal Balance,
	bool IsArchived,
	DateTime CreatedAt
);