using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Dtos;

public sealed record TransactionDetailsDto(
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	Currency? Currency,
	DirectionType Direction,
	bool IsExcluded
);
