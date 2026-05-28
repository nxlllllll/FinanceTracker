using FinanceTracker.Core.ReadModels;

namespace FinanceTracker.Core.Repositories.Transfer;

public interface ITransferReadRepository : IReadRepository<TransferReadModel>
{
	Task<TransferReadModel?> GetByIdAsync(
		Guid transferId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<TransferReadModel>> GetAllAsync(
		Guid userId,
		Guid? accountId = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
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