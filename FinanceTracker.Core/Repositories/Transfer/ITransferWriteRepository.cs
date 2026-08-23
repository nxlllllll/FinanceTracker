namespace FinanceTracker.Core.Repositories.Transfer;

public interface ITransferWriteRepository
{
	Task CreateAsync(
		Domains.Transfer.Transfer transfer,
		CancellationToken ct = default
	);

	Task SaveRateResolutionAsync(
		Domains.Transfer.Transfer transfer,
		CancellationToken ct = default
	);

	Task SaveStatusAsync(
		Domains.Transfer.Transfer transfer,
		CancellationToken ct = default
	);
}
