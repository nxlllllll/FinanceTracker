namespace FinanceTracker.Core.Repositories.Idempotency;

public interface IIdempotencyWriteRepository
{
	Task StoreAsync(
		Guid idempotencyKey,
		string commandType,
		string responseJson,
		DateTime expiresAt,
		CancellationToken ct = default
	);

	Task<int> DeleteExpiredAsync(
		DateTime before,
		int batchSize,
		CancellationToken ct = default
	);
}