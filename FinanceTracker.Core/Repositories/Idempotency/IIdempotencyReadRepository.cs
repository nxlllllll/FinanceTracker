namespace FinanceTracker.Core.Repositories.Idempotency;

public interface IIdempotencyReadRepository
{
	Task<string?> GetAsync(
		Guid idempotencyKey,
		CancellationToken ct = default
	);
}
