using FinanceTracker.Contracts.Events.Abstraction;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(Core.Domains.Account.Events.AccountCreated))]
public sealed record AccountCreatedEvent(
	Guid EventId,
	Guid AccountId,
	Guid UserId,
	string Name,
	string AccountType,
	string Currency,
	decimal Balance,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => AccountId;
}
