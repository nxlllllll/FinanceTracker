using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;

namespace FinanceTracker.Core.Dtos;

public sealed record UnresolvableEventDto(
	Guid Id,
	UnresolvableEventType Type,
	Guid ReferenceId,
	string Reason,
	DateTimeOffset OccurredAt
);
