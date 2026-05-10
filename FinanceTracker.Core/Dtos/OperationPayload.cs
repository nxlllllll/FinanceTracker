using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Dtos;

public abstract record OperationPayload;

public sealed record TransactionPayload(
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	Currency Currency,
	DirectionType Direction,
	bool IsExcluded
) : OperationPayload;

public sealed record TransferPayload(
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	Currency CurrencyFrom,
	decimal AmountTo,
	Currency CurrencyTo
) : OperationPayload;