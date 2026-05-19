using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.UnresolvableEvent;

public interface IUnresolvableEventReadRepository
{
	Task<IReadOnlyList<UnresolvableEventDto>> GetAllAsync(CancellationToken ct = default);
}