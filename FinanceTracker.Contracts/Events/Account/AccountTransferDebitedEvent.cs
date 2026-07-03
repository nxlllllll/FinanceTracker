using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountTransferDebited))]
public sealed record AccountTransferDebitedEvent(
	Guid EventId,
	Guid AccountId,
	Guid TransferId,
	Guid ToAccountId,
	decimal Amount,
	decimal ForexRate,
	string? Description,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => AccountId;
}
