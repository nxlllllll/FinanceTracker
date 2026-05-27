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
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		CancellationToken ct = default
	);
	
	Task<IReadOnlyList<PendingRateTransfer>> GetPendingRateAsync(CancellationToken ct = default);

	Task<int> GetPendingCreditCountAsync(
		TimeSpan gracePeriod,
		CancellationToken ct = default
	);
}
