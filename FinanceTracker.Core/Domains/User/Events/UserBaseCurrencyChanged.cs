using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.User.Events;

[EventType(name: "user.base_currency_changed")]
public sealed record UserBaseCurrencyChanged(
	Guid Id,
	Guid AggregateId,
	Currency OldBaseCurrency,
	Currency NewBaseCurrency,
	DateTime OccurredAt
) : IDomainEvent;