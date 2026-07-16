using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountRenamed))]
public sealed record AccountRenamedEvent(
	Guid EventId,
	Guid AccountId,
	string NewName,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => AccountId;
}
