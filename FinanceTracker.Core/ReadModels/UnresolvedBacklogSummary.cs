namespace FinanceTracker.Core.ReadModels;

public sealed record UnresolvedBacklogSummary(
	int TotalCount,
	DateTimeOffset? OldestOccurredAt,
	IReadOnlyList<UnresolvableEvent> Sample
) : IReadModel;