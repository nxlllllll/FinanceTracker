using FinanceTracker.Core.ReadModels.Currency;

namespace FinanceTracker.Core.Repositories.User;

public interface IBaseCurrencyRecalculationWriteRepository
{
	/// <summary>
	/// Marks a user's totals as needing a rebuild into <paramref name="targetCurrency"/>.
	/// </summary>
	Task RequestAsync(
		Guid userId,
		ValueObjects.Currency targetCurrency,
		DateTimeOffset requestedAt,
		CancellationToken ct = default
	);

	/// <summary>
	/// Takes a lease on the oldest claimable rows, returning what was claimed.
	/// </summary>
	Task<IReadOnlyList<BaseCurrencyRecalculation>> ClaimPendingBatchAsync(
		int batchSize,
		TimeSpan leaseDuration,
		DateTimeOffset now,
		CancellationToken ct = default
	);

	/// <summary>
	/// Marks a rebuild finished, but only if <paramref name="targetCurrency"/> is still what the row
	/// is aiming at. Returns false when it is not, which means the user changed currency again while
	/// this run was in flight and its results describe a currency nobody asked for any more.
	/// </summary>
	Task<bool> CompleteAsync(
		Guid userId,
		ValueObjects.Currency targetCurrency,
		CancellationToken ct = default
	);

	/// <summary>
	/// Records a failed attempt, releasing the lease so the row can be retried. Once
	/// <paramref name="maxAttempts"/> is reached the row is left <c>failed</c> and is not picked up
	/// again — an endless retry on a rebuild that never succeeds only hides it.
	/// </summary>
	Task FailAttemptAsync(
		Guid userId,
		string error,
		int maxAttempts,
		CancellationToken ct = default
	);
}
