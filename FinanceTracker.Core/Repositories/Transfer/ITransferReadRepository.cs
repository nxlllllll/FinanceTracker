using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.Transfer;

public interface ITransferReadRepository
{
	Task<Domains.Transfer.Transfer?> GetByIdAsync(
		Guid transferId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Domains.Transfer.Transfer>> GetAllAsync(
		Guid userId,
		Guid? accountId = null,
		DateTime? dateFrom = null,
		DateTime? dateTo = null,
		CancellationToken ct = default
	);
	
	Task<IReadOnlyList<PendingRateTransfer>> GetPendingRateAsync(
		CancellationToken ct = default
	);
}