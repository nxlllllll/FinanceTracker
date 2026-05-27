namespace FinanceTracker.Core.Repositories.UnresolvableEvent;

public interface IUnresolvableEventReadRepository
{
	Task<IReadOnlyList<UnresolvableEvent>> GetAllAsync(CancellationToken ct = default);
}
