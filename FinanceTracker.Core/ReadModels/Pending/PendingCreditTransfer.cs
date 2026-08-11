namespace FinanceTracker.Core.ReadModels.Pending;

public sealed record PendingCreditTransfer(
	Guid TransferId,
	Guid FromAccountId,
	decimal Amount,
	DateTimeOffset OccurredAt
) : IReadModel;
