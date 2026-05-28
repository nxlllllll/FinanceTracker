using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;

namespace FinanceTracker.Core.ReadModels;

public sealed record UnresolvableEvent(
	Guid Id,
	UnresolvableEventType Type,
	Guid ReferenceId,
	string Reason,
	DateTimeOffset OccurredAt
) : IReadModel;
