using FinanceTracker.Contracts.Events.Domain;

namespace FinanceTracker.Contracts.Events.User.Abstraction;

public interface IUserIntegrationEvent : IDomainIntegrationEvent
{
	Guid UserId { get; }
}
