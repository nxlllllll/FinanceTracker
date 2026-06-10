namespace FinanceTracker.Contracts.Events.Account.Abstraction;

/// <summary>
/// Marker interface for account integration events published to external consumers
/// via the outbox pattern. All account domain events that need to be exposed outside
/// the service boundary must have a corresponding implementation of this interface,
/// annotated with <see cref="FinanceTracker.Contracts.Events.IntegrationEventTypeAttribute"/>.
/// </summary>
public interface IAccountIntegrationEvent
{
	Guid EventId { get; }
	Guid AccountId { get; }
	int Version { get; }
	DateTimeOffset OccurredAt { get; }
}