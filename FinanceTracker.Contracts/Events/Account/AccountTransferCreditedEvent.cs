using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountTransferCredited))]
public sealed record AccountTransferCreditedEvent(
	Guid EventId,
	Guid AccountId,
	Guid TransferId,
	Guid FromAccountId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => AccountId;
}
