namespace FinanceTracker.Core.Repositories.UnresolvableEvent;

public interface IUnresolvableEventWriteRepository
{
	Task CreateAsync(
		Domains.Abstractions.UnresolvableEvent.UnresolvableEventType type,
		Guid referenceId,
		string reason,
		string payload,
		DateTimeOffset occurredAt,
		CancellationToken ct = default
	);

	Task AcknowledgeBatchAsync(
		IReadOnlyList<Guid> ids,
		DateTimeOffset acknowledgedAt,
		CancellationToken ct = default
	);

	Task ResolveAsync(
		Guid id,
		DateTimeOffset resolvedAt,
		CancellationToken ct = default
	);
}
