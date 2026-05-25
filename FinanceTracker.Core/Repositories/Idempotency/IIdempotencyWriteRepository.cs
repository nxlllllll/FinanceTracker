namespace FinanceTracker.Core.Repositories.Idempotency;

public interface IIdempotencyWriteRepository
{
	Task StoreAsync(
		Guid idempotencyKey,
		string commandType,
		string responseJson,
		DateTimeOffset expiresAt,
		CancellationToken ct = default
	);

	Task<int> DeleteExpiredAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default
	);
}
