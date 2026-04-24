using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Dtos;

public sealed record AccountDto(
	Guid Id,
	Guid UserId,
	string Name,
	AccountType Type,
	string Currency,
	decimal Balance,
	bool IsArchived,
	DateTime CreatedAt
);