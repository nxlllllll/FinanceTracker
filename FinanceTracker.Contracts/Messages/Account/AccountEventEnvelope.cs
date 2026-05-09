namespace FinanceTracker.Contracts.Messages.Account;

public sealed record AccountEventEnvelope(
	string EventType,
	string EventPayload
);