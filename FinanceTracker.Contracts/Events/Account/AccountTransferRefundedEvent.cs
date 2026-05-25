using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountTransferRefunded))]
public sealed record AccountTransferRefundedEvent(
	Guid EventId,
	Guid AccountId,
	Guid TransferId,
	decimal Amount,
	string? Description,
	DateTimeOffset OccurredAt
) : IAccountIntegrationEvent;
