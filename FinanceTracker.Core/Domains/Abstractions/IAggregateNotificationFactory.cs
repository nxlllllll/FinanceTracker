namespace FinanceTracker.Core.Domains.Abstractions;

public interface IAggregateNotificationFactory
{
	string AggregateType { get; }
	object Build(Guid aggregateId, IReadOnlyList<IEvent> events);
}