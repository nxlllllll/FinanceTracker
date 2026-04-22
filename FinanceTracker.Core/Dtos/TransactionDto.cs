using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Dtos;

public sealed record TransactionDto(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	DirectionType Direction,
	decimal ExchangeRate,
	bool IsExcluded,
	string? Description,
	DateTime OccurredAt
);