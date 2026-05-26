using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.User.Events;

[EventType(name: "user.registered")]
public sealed record UserRegistered(
	Guid Id,
	Guid AggregateId,
	Email Email,
	Currency BaseCurrency,
	DateTimeOffset OccurredAt
) : IDomainEvent;
