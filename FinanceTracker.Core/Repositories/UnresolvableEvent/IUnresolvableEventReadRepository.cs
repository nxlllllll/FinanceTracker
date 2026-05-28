namespace FinanceTracker.Core.Repositories.UnresolvableEvent;

public interface IUnresolvableEventReadRepository : IReadRepository<ReadModels.UnresolvableEvent>
{
	Task<IReadOnlyList<ReadModels.UnresolvableEvent>> GetAllAsync(CancellationToken ct = default);
}