namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

/// <summary>
/// Represents an immutable domain event that can be persisted in the event store.
/// Every event must be annotated with <see cref="EventTypeAttribute"/> and implement
/// <see cref="WithVersion"/> to support version stamping during persistence.
/// </summary>
public interface IEvent
{
	/// <summary>Unique identifier of this event instance.</summary>
	Guid Id { get; }

	/// <summary>
	/// The aggregate version after this event was applied.
	/// Assigned by the event store during <c>SaveAsync</c> — set to 0 when creating the event.
	/// </summary>
	int Version { get; }

	/// <summary>UTC timestamp when the event occurred in the domain.</summary>
	DateTimeOffset OccurredAt { get; }

	/// <summary>
	/// Returns a copy of this event with the given version number stamped in.
	/// Implement as <c>this with { Version = version }</c> on records.
	/// </summary>
	IEvent WithVersion(int version);
}