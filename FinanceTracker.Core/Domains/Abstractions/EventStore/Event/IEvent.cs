namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

public interface IEvent
{
	Guid Id { get; }
	int Version { get; }
	DateTimeOffset OccurredAt { get; }
	IEvent WithVersion(int version);
}