namespace FinanceTracker.Core.Domains.Abstractions.DomainEvent;

public interface IDomainEvent
{
	Guid Id { get; }
	Guid AggregateId { get; }
	DateTime OccurredAt { get; }
}