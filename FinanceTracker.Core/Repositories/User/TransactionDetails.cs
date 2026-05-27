using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Repositories.User;

public sealed record TransactionDetails(
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	ValueObjects.Currency? Currency,
	DirectionType Direction,
	bool IsExcluded
);
