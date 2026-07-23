namespace FinanceTracker.Core.Repositories.Idempotency;

public interface IIdempotencyReadRepository
{
	/// <summary>
	/// Looks up an idempotency record scoped to the exact (key, command type, user) triple.
	/// </summary>
	Task<IdempotencyEntry?> GetAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		CancellationToken ct = default
	);
}
