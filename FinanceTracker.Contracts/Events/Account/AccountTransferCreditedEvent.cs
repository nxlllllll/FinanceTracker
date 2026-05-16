using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountTransferCredited))]
public sealed record AccountTransferCreditedEvent(
	Guid AccountId,
	Guid TransferId,
	Guid FromAccountId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	DateTime OccurredAt
) : IAccountIntegrationEvent;