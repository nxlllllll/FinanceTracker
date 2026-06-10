namespace FinanceTracker.Core.Persistence;

/// <summary>
/// Coordinates a database transaction across one or more repository operations.
/// Supports nested transactions via savepoints — the inner scope commits to a savepoint,
/// and only the outermost scope commits the actual transaction.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
	/// <summary>
	/// Begins a transaction, or creates a savepoint if a transaction is already active.
	/// </summary>
	Task BeginTransactionAsync(CancellationToken ct = default);

	/// <summary>
	/// Saves changes and commits the transaction, or releases the savepoint for nested calls.
	/// Throws <c>UniqueConstraintException</c> on unique index violations (Postgres 23505).
	/// </summary>
	Task CommitAsync(CancellationToken ct = default);

	/// <summary>
	/// Rolls back the transaction, or rolls back to the savepoint for nested calls.
	/// </summary>
	Task RollbackAsync(CancellationToken ct = default);

	/// <summary>
	/// Executes <paramref name="operation"/> inside a transaction, committing on success
	/// and rolling back on any exception. Prefer this over manual Begin/Commit/Rollback.
	/// </summary>
	Task ExecuteInTransactionAsync(
		Func<Task> operation,
		CancellationToken ct = default
	);

	/// <summary>
	/// Same as <see cref="ExecuteInTransactionAsync(Func{Task}, CancellationToken)"/> but
	/// also invokes <paramref name="onError"/> with the exception before re-throwing.
	/// </summary>
	Task ExecuteInTransactionAsync(
		Func<Task> operation,
		Func<Exception, Task> onError,
		CancellationToken ct = default
	);
}