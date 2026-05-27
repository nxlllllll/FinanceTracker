namespace FinanceTracker.Core.Repositories.User;

public sealed record OperationRecord(
	Guid Id,
	OperationFilterType Type,
	string? Description,
	DateTimeOffset OccurredAt,
	TransactionDetails? Transaction,
	TransferDetails? Transfer
);
