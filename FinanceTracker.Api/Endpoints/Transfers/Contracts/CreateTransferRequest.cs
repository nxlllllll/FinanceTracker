namespace FinanceTracker.Api.Endpoints.Transfers.Contracts;

public sealed record CreateTransferRequest(
	Guid ToAccountId,
	decimal Amount,
	string? Description,
	DateTimeOffset OccurredAt
);
