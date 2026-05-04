namespace FinanceTracker.Core.Persistence;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
	Task BeginTransactionAsync(CancellationToken ct = default);
	
	Task CommitAsync(CancellationToken ct = default);
	
	Task RollbackAsync(CancellationToken ct = default);
	
	Task ExecuteInTransactionAsync(
		Func<Task> operation,
		CancellationToken ct = default
	);
	
	Task ExecuteInTransactionAsync(
		Func<Task> operation,
		Func<Exception, Task> onError,
		CancellationToken ct = default
	);
}