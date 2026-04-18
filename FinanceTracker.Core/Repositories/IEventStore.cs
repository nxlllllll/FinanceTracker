using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Repositories;

public interface IEventStore
{
	Task SaveAsync(
		Guid aggregateId,
		string aggregateType,
		IEnumerable<IEvent> events,
		int expectedVersion,
		CancellationToken ct = default
	);
	
	Task<IReadOnlyList<IEvent>> LoadAsync(
		Guid aggregateId,
		CancellationToken ct = default
	);
}