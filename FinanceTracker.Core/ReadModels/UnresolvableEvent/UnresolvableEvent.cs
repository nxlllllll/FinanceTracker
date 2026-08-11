using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;

namespace FinanceTracker.Core.ReadModels.UnresolvableEvent;

public sealed record UnresolvableEvent(
	Guid Id,
	UnresolvableEventType Type,
	Guid ReferenceId,
	string Reason,
	DateTimeOffset OccurredAt,
	DateTimeOffset? AcknowledgedAt,
	DateTimeOffset? ResolvedAt
) : IReadModel;
