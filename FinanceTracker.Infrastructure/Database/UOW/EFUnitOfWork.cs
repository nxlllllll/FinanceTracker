using FinanceTracker.Core.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceTracker.Infrastructure.Database.UOW;

public sealed class EFUnitOfWork(
	FinanceTrackerContext context
) : IUnitOfWork, IDisposable, IAsyncDisposable
{
	private IDbContextTransaction? _transaction;
	
	public async Task BeginTransactionAsync(CancellationToken ct = default)
		=> _transaction = await context.Database.BeginTransactionAsync(cancellationToken: ct);

	public async Task CommitAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
			throw new InvalidOperationException("No active transaction to commit.");
		
		await _transaction.CommitAsync(cancellationToken: ct);
		await _transaction.DisposeAsync();
		_transaction = null;
	}

	public async Task RollbackAsync(CancellationToken ct = default)
	{
		if (_transaction is null)
			return;

		await _transaction.RollbackAsync(cancellationToken: ct);
		await _transaction.DisposeAsync();
		_transaction = null;
	}

	public void Dispose()
	{
		_transaction?.Rollback();
		_transaction?.Dispose();
		_transaction = null;
	}

	public async ValueTask DisposeAsync()
	{
		if (_transaction is null)
			return;
		
		await _transaction.RollbackAsync();
		await _transaction.DisposeAsync();
		_transaction = null;
	}
}