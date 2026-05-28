using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record PendingRateTransaction(
	Guid TransactionId,
	Guid AccountId,
	decimal Amount,
	Currency TransactionCurrency,
	Currency BaseCurrency,
	decimal CurrentRate,
	DirectionType Direction,
	DateTimeOffset OccurredAt
) : IReadModel;