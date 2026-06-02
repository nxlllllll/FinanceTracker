namespace FinanceTracker.Core.Repositories.Idempotency;

public interface IIdempotencyWriteRepository
{
	Task<bool> TryReserveAsync(
		Guid idempotencyKey,
		string commandType,
		DateTimeOffset expiresAt,
		CancellationToken ct = default
	);

	Task CompleteAsync(
		Guid idempotencyKey,
		string responseJson,
		CancellationToken ct = default
	);

	Task<int> DeleteExpiredAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default
	);

	Task DeleteAsync(
		Guid idempotencyKey,
		CancellationToken ct = default
	);
}