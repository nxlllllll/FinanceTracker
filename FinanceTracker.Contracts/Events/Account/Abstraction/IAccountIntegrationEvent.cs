namespace FinanceTracker.Contracts.Events.Account.Abstraction;

public interface IAccountIntegrationEvent
{
	Guid EventId { get; }
	Guid AccountId { get; }
	DateTimeOffset OccurredAt { get; }
}
