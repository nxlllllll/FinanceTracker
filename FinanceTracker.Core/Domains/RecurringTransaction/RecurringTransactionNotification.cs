using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Domains.RecurringTransaction;

public record RecurringTransactionNotification(
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	string? Description,
	DateTime OccurredAt
);