namespace FinanceTracker.Contracts.Events.Account.Abstraction;

public interface IAccountIntegrationEvent
{
	Guid AccountId { get; }
	DateTime OccurredAt { get; }
}