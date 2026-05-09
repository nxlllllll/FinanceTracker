namespace FinanceTracker.Contracts.Messages.RecurringTransaction;

public sealed record RecurringTransactionTriggeredMessage(
	Guid MessageId,
	Guid RecurringTransactionId,
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	string Direction,
	string? Description,
	DateTime OccurredAt
);