using FinanceTracker.Core.Domains.Abstractions.DomainEvent;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;

namespace FinanceTracker.Core.Domains.User.Events;

[EventType(name: "user.password_changed")]
public sealed record UserPasswordChanged(
	Guid Id,
	Guid AggregateId,
	string NewPassword,
	DateTime OccurredAt
) : IDomainEvent;