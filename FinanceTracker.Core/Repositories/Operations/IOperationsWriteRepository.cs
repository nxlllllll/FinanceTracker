namespace FinanceTracker.Core.Repositories.Operations;

public interface IOperationsWriteRepository
{
	Task CreateFromTransactionAsync(
		Domains.Transaction.Transaction transaction,
		CancellationToken ct = default
	);
	
	Task CreateFromTransferAsync(
		Domains.Transfer.Transfer transfer, 
		CancellationToken ct = default
	);
	
	Task UpdateCategoryAsync(
		Guid operationId,
		Guid categoryId,
		CancellationToken ct = default
	);
	
	Task UpdateIsExcludedAsync(
		Guid operationId,
		bool isExcluded,
		CancellationToken ct = default
	);
	
	Task UpdateDescriptionAsync(
		Guid operationId,
		string? description,
		CancellationToken ct = default
	);
}