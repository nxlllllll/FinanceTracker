namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

public interface IEvent
{
	Guid Id { get; }
	DateTimeOffset OccurredAt { get; }
}
