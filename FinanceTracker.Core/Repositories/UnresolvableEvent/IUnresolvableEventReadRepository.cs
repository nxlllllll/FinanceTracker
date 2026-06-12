namespace FinanceTracker.Core.Repositories.UnresolvableEvent;

public interface IUnresolvableEventReadRepository : IReadRepository<ReadModels.UnresolvableEvent>
{
	Task<IReadOnlyList<ReadModels.UnresolvableEvent>> GetBatchAsync(
		int batchSize,
		DateTimeOffset? cursor = null,
		CancellationToken ct = default);
}