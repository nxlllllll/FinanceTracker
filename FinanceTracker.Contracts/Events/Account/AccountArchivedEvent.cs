using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountArchived))]
public sealed record AccountArchivedEvent(
	Guid EventId,
	Guid AccountId,
	int Version,
	DateTimeOffset OccurredAt
) : IAccountIntegrationEvent;
