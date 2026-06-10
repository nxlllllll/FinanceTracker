using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Infrastructure.EventMapping.Integration;

/// <summary>
/// Maps a domain event to its corresponding integration event for outbox publishing.
/// Returns <c>null</c> when no integration event is defined for the given domain event type —
/// those events are stored in the event store but not published externally.
/// </summary>
public interface IIntegrationEventMapper
{
	/// <summary>
	/// Maps <paramref name="event"/> to an <see cref="IAccountIntegrationEvent"/>,
	/// or <c>null</c> if the event has no external representation.
	/// </summary>
	IAccountIntegrationEvent? Map(IEvent @event);
}