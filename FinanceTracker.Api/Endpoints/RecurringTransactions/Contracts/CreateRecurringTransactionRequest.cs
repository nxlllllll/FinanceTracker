using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Api.Endpoints.RecurringTransactions.Contracts;

public sealed record CreateRecurringTransactionRequest(
	Guid AccountId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	int DayOfMonth,
	string? Description
);
