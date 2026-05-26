using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.User.Events;

[EventType(name: "user.email_changed")]
public sealed record UserEmailChanged(
	Guid Id,
	Guid AggregateId,
	Email OldEmail,
	Email NewEmail,
	DateTimeOffset OccurredAt
) : IDomainEvent;
