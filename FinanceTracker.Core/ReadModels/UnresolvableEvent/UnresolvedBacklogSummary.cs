namespace FinanceTracker.Core.ReadModels.UnresolvableEvent;

public sealed record UnresolvedBacklogSummary(
	int TotalCount,
	DateTimeOffset? OldestOccurredAt,
	IReadOnlyList<UnresolvableEvent> Sample
) : IReadModel;
