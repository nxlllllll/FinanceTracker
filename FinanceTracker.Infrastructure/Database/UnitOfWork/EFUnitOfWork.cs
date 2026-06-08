using FinanceTracker.Core.Persistence;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.UnitOfWork;

public sealed class EFUnitOfWork(
	FinanceTrackerContext context,
	ILogger<EFUnitOfWork> logger
) : IUnitOfWork
{
	private IDbContextTransaction? _transaction;
	private readonly Stack<string> _savepoints = new Stack<string>();

	public async Task BeginTransactionAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
		{
			_transaction = await context.Database.BeginTransactionAsync(cancellationToken: ct);
			return;
		}

		string savepointName = $"sp_{Guid.CreateVersion7():N}";
		await _transaction.CreateSavepointAsync(name: savepointName, cancellationToken: ct);
		_savepoints.Push(item: savepointName);
	}

	public async Task CommitAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
		{
			logger.ZLogError(message: $"Commit called without an active transaction.");
			throw new InvalidOperationException(message: "No active transaction to commit.");
		}

		await context.SaveChangesAsync(cancellationToken: ct);

		if (_savepoints.TryPop(result: out string? savepointName))
		{
			await _transaction.ReleaseSavepointAsync(name: savepointName, cancellationToken: ct);
			return;
		}

		await _transaction.CommitAsync(cancellationToken: ct);
		await _transaction.DisposeAsync();
		_transaction = null;
	}

	public async Task RollbackAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
			return;

		if (_savepoints.TryPop(result: out string? savepointName))
		{
			await _transaction.RollbackToSavepointAsync(name: savepointName, cancellationToken: ct);
			context.ChangeTracker.Clear();
			return;
		}

		await _transaction.RollbackAsync(cancellationToken: ct);
		await _transaction.DisposeAsync();
		_transaction = null;
		context.ChangeTracker.Clear();
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

	public async ValueTask DisposeAsync()
	{
		if (_transaction is null)
			return;

		_savepoints.Clear();
		await _transaction.RollbackAsync();
		await _transaction.DisposeAsync();
		_transaction = null;
		context.ChangeTracker.Clear();
	}
}