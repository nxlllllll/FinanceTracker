using FinanceTracker.Core.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceTracker.Infrastructure.Database.UOW;

public sealed class EFUnitOfWork(
	FinanceTrackerContext context
) : IUnitOfWork
{
	private IDbContextTransaction? _transaction;
	private int _transactionDepth = 0;
	
	public async Task BeginTransactionAsync(CancellationToken ct = default)
	{
		++_transactionDepth;

		if (_transactionDepth == 1)
			_transaction = await context.Database.BeginTransactionAsync(cancellationToken: ct);
		else
		{
			if (_transaction is null)
				throw new InvalidOperationException(message: $"Transaction is null at depth {_transactionDepth}.");
			
			await _transaction.CreateSavepointAsync(name: $"Savepoint_{_transactionDepth}", cancellationToken: ct);
		}
	}

	public async Task CommitAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
			throw new InvalidOperationException(message: "No active transaction to commit.");
		
		--_transactionDepth;
		
		if (_transactionDepth != 0)
			return;
		
		await _transaction.CommitAsync(cancellationToken: ct);
		await _transaction.DisposeAsync();
		_transaction = null;
	}

	public async Task RollbackAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
			return;

		if (_transactionDepth > 1)
		{
			await _transaction.RollbackToSavepointAsync(name: $"Savepoint_{_transactionDepth}", cancellationToken: ct);
			--_transactionDepth;
		}
		else
		{
			_transactionDepth = 0;
			await _transaction.RollbackAsync(cancellationToken: ct);
			await _transaction.DisposeAsync();
			_transaction = null;
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

	public void Dispose()
	{
		if (_transaction is null)
			return;

		_transactionDepth = 0;
		_transaction.Rollback();
		_transaction.Dispose();
		_transaction = null;
	}

	public async ValueTask DisposeAsync()
	{
		if (_transaction is null)
			return;
		
		_transactionDepth = 0;
		await _transaction.RollbackAsync();
		await _transaction.DisposeAsync();
		_transaction = null;
	}
}