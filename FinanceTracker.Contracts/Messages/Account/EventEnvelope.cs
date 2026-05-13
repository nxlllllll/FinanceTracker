namespace FinanceTracker.Contracts.Messages.Account;

public sealed record EventEnvelope(
	string EventType,
	string EventPayload
);