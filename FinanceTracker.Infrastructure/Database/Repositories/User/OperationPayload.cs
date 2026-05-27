using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public abstract record OperationPayload;

public sealed record TransactionPayload(
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	Core.ValueObjects.Currency Currency,
	DirectionType Direction,
	bool IsExcluded
) : OperationPayload;

public sealed record TransferPayload(
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	Core.ValueObjects.Currency CurrencyFrom,
	decimal AmountTo,
	Core.ValueObjects.Currency CurrencyTo
) : OperationPayload;
