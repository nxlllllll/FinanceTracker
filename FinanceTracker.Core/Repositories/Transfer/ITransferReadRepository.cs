using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.Transfer;

public interface ITransferReadRepository : IReadRepository<TransferReadModel>
{
	Task<TransferReadModel?> GetByIdAsync(
		Guid transferId,
		CancellationToken ct = default
	);

	Task<PagedResult<TransferReadModel>> GetAllAsync(
		Guid userId,
		Guid? accountId = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<PendingRateTransfer>> GetPendingRateAsync(
		CancellationToken ct = default
	);

	Task<int> GetPendingCreditCountAsync(
		TimeSpan gracePeriod,
		CancellationToken ct = default
	);
}