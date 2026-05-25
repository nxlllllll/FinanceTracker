using FinanceTracker.Contracts.Events.User.Abstraction;
using FinanceTracker.Core.Domains.User.Events;

namespace FinanceTracker.Contracts.Events.User;

[IntegrationEventType(domainEventType: typeof(UserRegistered))]
public sealed record UserRegisteredEvent(
	Guid EventId,
	Guid UserId,
	string Email,
	string BaseCurrency,
	DateTimeOffset OccurredAt
) : IUserIntegrationEvent;
