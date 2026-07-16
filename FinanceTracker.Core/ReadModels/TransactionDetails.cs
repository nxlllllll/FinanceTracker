using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record TransactionDetails(
	Guid? AccountId,
	Guid CategoryId,
	decimal Amount,
	Currency? Currency,
	DirectionType Direction,
	bool IsExcluded
) : IReadModel;
