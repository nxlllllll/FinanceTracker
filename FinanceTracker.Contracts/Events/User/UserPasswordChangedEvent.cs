using FinanceTracker.Contracts.Events.User.Abstraction;
using FinanceTracker.Core.Domains.User.Events;

namespace FinanceTracker.Contracts.Events.User;

[IntegrationEventType(domainEventType: typeof(UserBaseCurrencyChanged))]
public sealed record UserPasswordChangedEvent(
	Guid EventId,
	Guid UserId,
	string NewPassword,
	DateTime OccurredAt
) : IUserIntegrationEvent;