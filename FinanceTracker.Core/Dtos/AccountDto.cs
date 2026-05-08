using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Dtos;

public sealed record AccountDto(
	Guid Id,
	Guid UserId,
	string Name,
	AccountType Type,
	Currency Currency,
	decimal Balance,
	bool IsArchived,
	DateTime CreatedAt
);