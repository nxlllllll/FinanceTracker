using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountRenamed))]
public sealed record AccountRenamedEvent(
	Guid EventId,
	Guid AccountId,
	string NewName,
	int Version,
	DateTimeOffset OccurredAt
) : IAccountIntegrationEvent;
