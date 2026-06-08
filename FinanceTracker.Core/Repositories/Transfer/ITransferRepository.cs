namespace FinanceTracker.Core.Repositories.Transfer;

public interface ITransferRepository
{
	Task<Domains.Transfer.Transfer?> GetByIdAsync(
		Guid transferId,
		CancellationToken ct = default
	);
}