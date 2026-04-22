namespace FinanceTracker.Core.Domains.Abstractions;

public sealed record AggregateNotification(
	Guid AggregateId,
	string AggregateType,
	IReadOnlyList<IEvent> Events
);