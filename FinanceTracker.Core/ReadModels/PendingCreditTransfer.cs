namespace FinanceTracker.Core.ReadModels;

public sealed record PendingCreditTransfer(
	Guid TransferId,
	Guid FromAccountId,
	decimal Amount,
	DateTimeOffset OccurredAt
) : IReadModel;