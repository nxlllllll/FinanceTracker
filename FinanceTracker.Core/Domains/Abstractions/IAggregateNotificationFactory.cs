namespace FinanceTracker.Core.Domains.Abstractions;

public interface IAggregateNotificationFactory
{
	string AggregateType { get; }
	IAppNotification Build(Guid aggregateId, IReadOnlyList<IEvent> events);
}