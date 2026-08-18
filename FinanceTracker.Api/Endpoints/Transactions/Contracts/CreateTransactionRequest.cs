using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Api.Endpoints.Transactions.Contracts;

public sealed record CreateTransactionRequest(
	Guid CategoryId,
	decimal Amount,
	string Currency,
	DirectionType Direction,
	string? Description,
	DateTimeOffset OccurredAt
);
