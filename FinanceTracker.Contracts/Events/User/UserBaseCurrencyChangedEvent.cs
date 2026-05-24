using FinanceTracker.Contracts.Events.User.Abstraction;
using FinanceTracker.Core.Domains.User.Events;

namespace FinanceTracker.Contracts.Events.User;

[IntegrationEventType(domainEventType: typeof(UserBaseCurrencyChanged))]
public sealed record UserBaseCurrencyChangedEvent(
	Guid EventId,
	Guid UserId,
	string NewBaseCurrency,
	DateTime OccurredAt
) : IUserIntegrationEvent;