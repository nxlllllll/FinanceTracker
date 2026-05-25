using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;

namespace FinanceTracker.Core.Repositories.UnresolvableEvent;

public interface IUnresolvableEventWriteRepository
{
	Task CreateAsync(
		UnresolvableEventType type,
		Guid referenceId,
		string reason,
		string payload,
		DateTimeOffset occurredAt,
		CancellationToken ct = default
	);
}
