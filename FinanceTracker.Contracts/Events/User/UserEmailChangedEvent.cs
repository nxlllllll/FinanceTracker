using FinanceTracker.Contracts.Events.User.Abstraction;
using FinanceTracker.Core.Domains.User.Events;

namespace FinanceTracker.Contracts.Events.User;

[IntegrationEventType(domainEventType: typeof(UserEmailChanged))]
public sealed record UserEmailChangedEvent(
	Guid EventId,
	Guid UserId,
	string NewEmail,
	DateTime OccurredAt
) : IUserIntegrationEvent;