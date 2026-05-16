using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountCredited))]
public sealed record AccountCreditedEvent(
	Guid AccountId,
	Guid TransactionId,
	Guid CategoryId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	DateTime OccurredAt
) : IAccountIntegrationEvent;