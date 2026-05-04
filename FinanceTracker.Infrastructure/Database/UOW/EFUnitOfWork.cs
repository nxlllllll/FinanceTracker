using FinanceTracker.Core.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceTracker.Infrastructure.Database.UOW;

public sealed class EFUnitOfWork(
	FinanceTrackerContext context
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
 
		string savepointName = $"sp_{Guid.NewGuid():N}";
		await _transaction.CreateSavepointAsync(name: savepointName, cancellationToken: ct);
		_savepoints.Push(item: savepointName);
	}

	public async Task CommitAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
			throw new InvalidOperationException(message: "No active transaction to commit.");
 
		if (_savepoints.Count > 0)
		{
			_savepoints.Pop();
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
			return;
		}
 
		await _transaction.RollbackAsync(cancellationToken: ct);
		await _transaction.DisposeAsync();
		_transaction = null;
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
		}
	}

	public void Dispose()
	{
		if (_transaction is null)
			return;

		_savepoints.Clear();
		_transaction.Rollback();
		_transaction.Dispose();
		_transaction = null;
	}

	public async ValueTask DisposeAsync()
	{
		if (_transaction is null)
			return;
		
		_savepoints.Clear();
		await _transaction.RollbackAsync();
		await _transaction.DisposeAsync();
		_transaction = null;
	}
}