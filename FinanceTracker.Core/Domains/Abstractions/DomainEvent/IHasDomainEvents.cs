namespace FinanceTracker.Core.Domains.Abstractions.DomainEvent;

public interface IHasDomainEvents
{
	string AggregateType { get; }
	IReadOnlyList<IDomainEvent> DomainEvents { get; }
	void ClearDomainEvents();
}
