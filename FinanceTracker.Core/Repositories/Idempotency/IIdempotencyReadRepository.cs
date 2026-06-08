namespace FinanceTracker.Core.Repositories.Idempotency;

public interface IIdempotencyReadRepository
{
	Task<IdempotencyEntry?> GetAsync(Guid idempotencyKey, CancellationToken ct = default);
}