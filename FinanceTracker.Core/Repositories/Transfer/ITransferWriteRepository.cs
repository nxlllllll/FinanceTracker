namespace FinanceTracker.Core.Repositories.Transfer;

public interface ITransferWriteRepository
{
	Task CreateAsync(
		Core.Domains.Transfer.Transfer transfer,
		CancellationToken ct = default
	);

	Task SaveRateResolutionAsync(
		Core.Domains.Transfer.Transfer transfer,
		CancellationToken ct = default
	);

	Task SaveStatusAsync(
		Core.Domains.Transfer.Transfer transfer,
		CancellationToken ct = default
	);
}
