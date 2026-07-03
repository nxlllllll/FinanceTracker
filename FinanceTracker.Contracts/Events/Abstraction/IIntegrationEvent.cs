namespace FinanceTracker.Contracts.Events.Abstraction;

/// <summary>
/// Marker interface for integration events published to external consumers
/// via the outbox pattern. Any domain event that needs to be exposed outside
/// the service boundary must have a corresponding implementation of this interface,
/// annotated with <see cref="FinanceTracker.Contracts.Events.IntegrationEventTypeAttribute"/>.
/// </summary>
public interface IIntegrationEvent
{
	Guid EventId { get; }
	Guid AggregateId { get; }
	int Version { get; }
	DateTimeOffset OccurredAt { get; }
}
