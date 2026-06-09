namespace FinanceTracker.Core.Services.Rebuild;

public interface IAccountProjectionRebuilder
{
	Task RebuildAsync(Guid accountId, CancellationToken ct = default);

	Task RebuildAllAsync(int batchSize = 50, CancellationToken ct = default);
}