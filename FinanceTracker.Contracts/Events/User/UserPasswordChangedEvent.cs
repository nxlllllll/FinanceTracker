using FinanceTracker.Contracts.Events.User.Abstraction;
using FinanceTracker.Core.Domains.User.Events;

namespace FinanceTracker.Contracts.Events.User;

[IntegrationEventType(domainEventType: typeof(UserPasswordChanged))]
public sealed record UserPasswordChangedEvent(
	Guid EventId,
	Guid UserId,
	DateTimeOffset OccurredAt
) : IUserIntegrationEvent;
