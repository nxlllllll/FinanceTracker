using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountCredited))]
public sealed record AccountCreditedEvent(
	Guid EventId,
	Guid AccountId,
	Guid TransactionId,
	Guid CategoryId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	int Version,
	DateTimeOffset OccurredAt
) : IIntegrationEvent
{
	Guid IIntegrationEvent.AggregateId => AccountId;
}
