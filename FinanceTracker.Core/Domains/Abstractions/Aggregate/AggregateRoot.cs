using FinanceTracker.Core.Domains.Abstractions.ES.Event;

namespace FinanceTracker.Core.Domains.Abstractions.Aggregate;

public abstract class AggregateRoot
{
	private readonly List<IEvent> _events = [];

	public Guid Id { get; protected set; }
	public int Version { get; private set; }
	public IReadOnlyList<IEvent> Events => _events.AsReadOnly();

	private void Load(IEvent @event)
	{
		Apply(@event: @event);
		++Version;
	}

	protected abstract void Apply(IEvent @event);

	protected void RaiseEvent(IEvent @event)
	{
		Load(@event: @event);
		_events.Add(item: @event);
	}

	protected void RestoreVersion(int version)
		=> Version = version;
	
	internal void LoadEventsFromHistory(IReadOnlyList<IEvent> history)
	{
		foreach (IEvent @event in history)
			Load(@event: @event);
	}

	public void ClearEvents()
		=> _events.Clear();
}
