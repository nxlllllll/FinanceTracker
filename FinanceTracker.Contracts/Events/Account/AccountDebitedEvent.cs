using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(AccountDebited))]
public sealed record AccountDebitedEvent(
	Guid AccountId,
	Guid TransactionId,
	Guid CategoryId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	DateTime OccurredAt
) : IAccountIntegrationEvent;