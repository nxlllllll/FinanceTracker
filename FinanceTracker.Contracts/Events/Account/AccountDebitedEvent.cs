using FinanceTracker.Contracts.Events.Account.Abstraction;

namespace FinanceTracker.Contracts.Events.Account;

[IntegrationEventType(domainEventType: typeof(Core.Domains.Account.Events.AccountDebited))]
public sealed record AccountDebitedEvent(
	Guid EventId,
	Guid AccountId,
	Guid TransactionId,
	Guid CategoryId,
	decimal Amount,
	decimal ExchangeRate,
	string? Description,
	int Version,
	DateTimeOffset OccurredAt
) : IAccountIntegrationEvent;