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

	/// <summary>
	/// Executes <paramref name="operation"/> inside a transaction and returns its result,
	/// committing on success and rolling back on any exception.
	/// Use this overload when the operation needs to return a value — avoids mutating
	/// a captured variable from a closure.
	/// </summary>
	Task<T> ExecuteInTransactionAsync<T>(
		Func<Task<T>> operation,
		CancellationToken ct = default
	);

	/// <summary>
	/// Same as <see cref="ExecuteInTransactionAsync{T}(Func{Task{T}}, CancellationToken)"/> but
	/// also invokes <paramref name="onError"/> with the exception before re-throwing.
	/// </summary>
	Task<T> ExecuteInTransactionAsync<T>(
		Func<Task<T>> operation,
		Func<Exception, Task> onError,
		CancellationToken ct = default
	);

	/// <summary>
	/// Registers <paramref name="callback"/> to run once the outermost transaction actually
	/// commits — never for a nested (savepoint) commit, and never if the transaction rolls back.
	/// </summary>
	void OnCommitted(Action callback);

	/// <summary>
	/// Same as <see cref="OnCommitted(Action)"/>, but for callbacks that need to await
	/// asynchronous work — e.g. publishing a MediatR notification after a real commit.
	/// Use this instead of wrapping an async call in a synchronous <see cref="Action"/>:
	/// blocking on async code is banned in this codebase (see <c>BannedSymbols.txt</c>), and an
	/// un-awaited fire-and-forget <c>Task</c> would silently swallow both completion and
	/// exceptions raised by <paramref name="callback"/>.
	/// </summary>
	void OnCommitted(Func<Task> callback);
}
