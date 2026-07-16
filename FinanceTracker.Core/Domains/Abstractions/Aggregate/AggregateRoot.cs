using System.Collections.ObjectModel;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Abstractions.Aggregate;

/// <summary>
/// Base class for event-sourced aggregate roots.
/// Maintains an in-memory list of uncommitted events raised during the current operation,
/// and tracks the aggregate version for optimistic concurrency control.
/// </summary>
/// <remarks>
/// Typical usage:
/// <list type="number">
///   <item>Call a domain method that invokes <see cref="RaiseEvent"/> internally.</item>
///   <item>Pass <see cref="Events"/> to <c>IEventStore.SaveAsync</c>.</item>
/// </list>
/// </remarks>
public abstract class AggregateRoot
{
	private readonly List<IEvent> _events = [];

	private ReadOnlyCollection<IEvent>? _eventsReadOnly;

	/// <summary>Unique identifier of this aggregate.</summary>
	public Guid Id { get; protected set; }

	/// <summary>
	/// Current version — incremented by one for each event applied, including those
	/// loaded from history. Used as the optimistic concurrency token.
	/// </summary>
	public int Version { get; private set; }

	/// <summary>
	/// The version at which the aggregate was last persisted.
	/// Equals <c>Version - Events.Count</c>.
	/// </summary>
	public int PersistedVersion => Version - _events.Count;

	/// <summary>Uncommitted events raised since the <c>Repository.SaveAsync</c> call.</summary>
	public IReadOnlyList<IEvent> Events => _eventsReadOnly ??= _events.AsReadOnly();

	private void Load(IEvent @event)
	{
		Apply(@event: @event);
		++Version;
	}

	/// <summary>
	/// Applies a single event to the aggregate state.
	/// Implement as a <c>switch</c> or pattern match on the event type.
	/// </summary>
	protected abstract void Apply(IEvent @event);

	/// <summary>
	/// Applies the event to state, increments <see cref="Version"/>,
	/// and adds the version-stamped event to <see cref="Events"/>.
	/// Call this from domain methods to record state changes.
	/// </summary>
	protected void RaiseEvent(IEvent @event)
	{
		Load(@event: @event);
		IEvent versioned = @event.WithVersion(version: Version);
		_events.Add(item: versioned);
	}

	/// <summary>
	/// Restores the aggregate version directly from a snapshot.
	/// Used during reconstitution when a snapshot is available.
	/// </summary>
	protected void RestoreVersion(int version)
		=> Version = version;

	/// <summary>
	/// Replays historical events to rebuild aggregate state without adding them
	/// to the uncommitted events list.
	/// </summary>
	internal void LoadEventsFromHistory(IReadOnlyList<IEvent> history)
	{
		foreach (IEvent @event in history)
			Load(@event: @event);
	}

	/// <summary>
	/// Clears the uncommitted events list. Call after a successful <c>SaveAsync</c>.
	/// </summary>
	public void ClearEvents()
		=> _events.Clear();
}
