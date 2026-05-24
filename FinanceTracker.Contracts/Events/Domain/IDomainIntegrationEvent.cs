namespace FinanceTracker.Contracts.Events.Domain;

public interface IDomainIntegrationEvent
{
	Guid EventId { get; }
	DateTime OccurredAt { get; }
}