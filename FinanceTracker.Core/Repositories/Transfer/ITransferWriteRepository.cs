using FinanceTracker.Core.Domains.Transfer;

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
		int expectedVersion,
		CancellationToken ct = default
	);

	Task UpdateStatusAsync(
		Guid transferId,
		Guid userId,
		TransferStatus status,
		int expectedVersion,
		CancellationToken ct = default
	);
}
