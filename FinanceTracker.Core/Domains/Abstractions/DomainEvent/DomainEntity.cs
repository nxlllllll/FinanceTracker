namespace FinanceTracker.Core.Domains.Abstractions.DomainEvent;

public abstract class DomainEntity : IHasDomainEvents
{
	private readonly List<IDomainEvent> _domainEvents = [];
	public abstract string AggregateType { get; }
	public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
	public void ClearDomainEvents() => _domainEvents.Clear();

	protected void RaiseDomainEvent(IDomainEvent @event) => _domainEvents.Add(item: @event);
}