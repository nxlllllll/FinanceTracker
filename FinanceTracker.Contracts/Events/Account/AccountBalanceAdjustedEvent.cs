using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountBalanceAdjusted))]
public sealed record AccountBalanceAdjustedEvent(
	Guid EventId,
	Guid AccountId,
	Guid SourceId,
	string SourceType,
	decimal OldRate,
	decimal NewRate,
	decimal Amount,
	decimal Delta,
	int Version,
	DateTimeOffset OccurredAt
) : IAccountIntegrationEvent;
