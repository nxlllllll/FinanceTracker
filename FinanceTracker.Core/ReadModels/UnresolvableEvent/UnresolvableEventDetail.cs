using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;

namespace FinanceTracker.Core.ReadModels.UnresolvableEvent;

public sealed record UnresolvableEventDetail(
	Guid Id,
	UnresolvableEventType Type,
	Guid ReferenceId,
	string Reason,
	string Payload,
	DateTimeOffset OccurredAt,
	DateTimeOffset? AcknowledgedAt,
	DateTimeOffset? ResolvedAt
) : IReadModel;
