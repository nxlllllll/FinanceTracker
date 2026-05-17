namespace FinanceTracker.Core.Repositories.Transfer;

public interface ITransferWriteRepository
{
	Task CreateAsync(
		Domains.Transfer.Transfer transfer,
		CancellationToken ct = default
	);
	
	Task UpdateRateAsync(
		Guid transferId,
		decimal newRate,
		CancellationToken ct = default
	);
}