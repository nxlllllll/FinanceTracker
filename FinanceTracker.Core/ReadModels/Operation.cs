namespace FinanceTracker.Core.ReadModels;

public sealed record Operation(
	Guid Id,
	OperationFilterType Type,
	string? Description,
	DateTimeOffset OccurredAt,
	TransactionDetails? Transaction,
	TransferDetails? Transfer
) : IReadModel;