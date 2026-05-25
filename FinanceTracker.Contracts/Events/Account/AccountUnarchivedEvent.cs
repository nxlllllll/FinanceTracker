using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountUnarchived))]
public sealed record AccountUnarchivedEvent(
	Guid EventId,
	Guid AccountId,
	DateTimeOffset OccurredAt
) : IAccountIntegrationEvent;
