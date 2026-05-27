using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Repositories.Transaction;

public sealed record PendingRateTransaction(
	Guid TransactionId,
	Guid AccountId,
	decimal Amount,
	ValueObjects.Currency TransactionCurrency,
	ValueObjects.Currency BaseCurrency,
	decimal CurrentRate,
	DirectionType Direction,
	DateTimeOffset OccurredAt
);
