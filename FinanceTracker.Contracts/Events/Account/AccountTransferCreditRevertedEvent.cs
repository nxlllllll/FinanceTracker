using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountTransferCreditReverted))]
public sealed record AccountTransferCreditRevertedEvent(
	Guid EventId,
	Guid AccountId,
	Guid TransferId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => AccountId;
}
