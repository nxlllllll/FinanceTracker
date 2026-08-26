using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.ReadModels.Transfer;

namespace FinanceTracker.Core.ReadModels.Operation;

public sealed record Operation(
	Guid Id,
	OperationFilterType Type,
	string? Description,
	DateTimeOffset OccurredAt,
	bool IsReverted,
	Guid? ReversalOfId,
	TransactionDetails? Transaction,
	TransferDetails? Transfer
) : IReadModel;
