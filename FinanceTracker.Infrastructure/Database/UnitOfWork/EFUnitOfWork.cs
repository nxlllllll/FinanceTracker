using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.EventStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.UnitOfWork;

public sealed class EFUnitOfWork(
	FinanceTrackerContext context,
	ILogger<EFUnitOfWork> logger
) : IUnitOfWork
{
	private const string PostgresUniqueViolationCode = "23505";
	private const string PostgresExclusionViolationCode = "23P01";

	private IDbContextTransaction? _transaction;
	private readonly Stack<string> _savepoints = new Stack<string>();

	private readonly Stack<HashSet<object>> _savepointSnapshots = new Stack<HashSet<object>>();

	private List<Action> _onCommittedCallbacks = [];

	public void OnCommitted(Action callback)
		=> _onCommittedCallbacks.Add(item: callback);

	public async Task BeginTransactionAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
		{
			_transaction = await context.Database.BeginTransactionAsync(cancellationToken: ct);
			return;
		}

		HashSet<object> snapshot = new HashSet<object>(
			collection: context.ChangeTracker.Entries().Select(selector: e => e.Entity),
			comparer: ReferenceEqualityComparer.Instance
		);

		string savepointName = $"sp_{Guid.CreateVersion7():N}";
		await _transaction.CreateSavepointAsync(name: savepointName, cancellationToken: ct);
		_savepoints.Push(item: savepointName);
		_savepointSnapshots.Push(item: snapshot);
	}

	public async Task CommitAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
		{
			logger.ZLogError(message: $"Commit called without an active transaction.");
			throw new InvalidOperationException(message: "No active transaction to commit.");
		}

		try
		{
			await context.SaveChangesAsync(cancellationToken: ct);
		}
		catch (DbUpdateConcurrencyException ex)
		{
			Guid aggregateId = ex.Entries[0].Property(propertyName: "Id").CurrentValue is Guid id ? id : Guid.Empty;
			logger.ZLogWarning(exception: ex, message: $"Concurrency conflict on entity {ex.Entries[0].Metadata.Name} {aggregateId}.");
			throw new ConcurrencyConflictException(message: "Conflict: the record was modified by another request.", id: aggregateId);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException
		{
			SqlState: PostgresUniqueViolationCode,
			ConstraintName: EventEntityConfiguration.VersionConstraint
		})
		{
			Guid aggregateId = ex.Entries[0].Property(propertyName: "AggregateId").CurrentValue is Guid id ? id : Guid.Empty;
			logger.ZLogWarning(exception: ex, message: $"Concurrency conflict on event store aggregate {aggregateId}.");
			throw new ConcurrencyConflictException(message: "Conflict: the aggregate was modified by another request.", id: aggregateId);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresUniqueViolationCode } pgEx)
		{
			throw new UniqueConstraintException(message: "A record with the same unique key already exists.", constraintName: pgEx.ConstraintName ?? String.Empty);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresExclusionViolationCode } pgEx)
		{
			throw new UniqueConstraintException(
				message: "A record conflicting with an existing one (e.g. an overlapping range) already exists.",
				constraintName: pgEx.ConstraintName ?? String.Empty
			);
		}

		if (_savepoints.TryPop(result: out string? savepointName))
		{
			await _transaction.ReleaseSavepointAsync(name: savepointName, cancellationToken: ct);
			_savepointSnapshots.Pop();
			return;
		}

		await _transaction.CommitAsync(cancellationToken: ct);
		await _transaction.DisposeAsync();
		_transaction = null;

		List<Action> callbacks = _onCommittedCallbacks;
		_onCommittedCallbacks = [];
		foreach (Action callback in callbacks)
			callback();
	}

	public async Task RollbackAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
			return;

		_onCommittedCallbacks = [];

		if (_savepoints.TryPop(result: out string? savepointName))
		{
			await _transaction.RollbackToSavepointAsync(name: savepointName, cancellationToken: ct);

			HashSet<object> snapshot = _savepointSnapshots.Pop();
			await ReconcileChangeTrackerAsync(snapshotBeforeSavepoint: snapshot, ct: ct);
			return;
		}

		await _transaction.RollbackAsync(cancellationToken: ct);
		await _transaction.DisposeAsync();
		_transaction = null;
		context.ChangeTracker.Clear();
	}

	/// <summary>
	/// Brings the <c>ChangeTracker</c> back in sync with the database after a savepoint rollback,
	/// instead of either leaving stale entries behind or wiping tracking the outer scope still needs.
	/// </summary>
	private async Task ReconcileChangeTrackerAsync(HashSet<object> snapshotBeforeSavepoint, CancellationToken ct)
	{
		foreach (EntityEntry entry in context.ChangeTracker.Entries().ToList())
		{
			if (snapshotBeforeSavepoint.Contains(item: entry.Entity))
				await entry.ReloadAsync(cancellationToken: ct);
			else
				entry.State = EntityState.Detached;
		}
	}

	public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
	{
		await BeginTransactionAsync(ct: ct);
		try
		{
			await operation();
			await CommitAsync(ct: ct);
		}
		catch
		{
			await RollbackAsync(ct: ct);
			throw;
		}
	}

	public async Task ExecuteInTransactionAsync(
		Func<Task> operation,
		Func<Exception, Task> onError,
		CancellationToken ct = default)
	{
		await BeginTransactionAsync(ct: ct);
		try
		{
			await operation();
			await CommitAsync(ct: ct);
		}
		catch (Exception e)
		{
			await RollbackAsync(ct: ct);
			await onError(arg: e);
			throw;
		}
	}

	public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
	{
		await BeginTransactionAsync(ct: ct);
		try
		{
			T result = await operation();
			await CommitAsync(ct: ct);
			return result;
		}
		catch
		{
			await RollbackAsync(ct: ct);
			throw;
		}
	}

	public async Task<T> ExecuteInTransactionAsync<T>(
		Func<Task<T>> operation,
		Func<Exception, Task> onError,
		CancellationToken ct = default)
	{
		await BeginTransactionAsync(ct: ct);
		try
		{
			T result = await operation();
			await CommitAsync(ct: ct);
			return result;
		}
		catch (Exception e)
		{
			await RollbackAsync(ct: ct);
			await onError(arg: e);
			throw;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_transaction is null)
			return;

		_savepoints.Clear();
		_savepointSnapshots.Clear();
		_onCommittedCallbacks = [];
		await _transaction.RollbackAsync();
		await _transaction.DisposeAsync();
		_transaction = null;
		context.ChangeTracker.Clear();
	}
}
