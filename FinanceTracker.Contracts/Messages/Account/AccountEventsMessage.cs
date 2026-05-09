namespace FinanceTracker.Contracts.Messages.Account;

public sealed record AccountEventsMessage(
	Guid MessageId,
	Guid AggregateId,
	IReadOnlyList<AccountEventEnvelope> Events
);