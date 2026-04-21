using FinanceTracker.Core.Domains.Transactions.Events;

namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionWriteRepository
{
	Task CreateAsync(
		TransactionCreated @event,
		CancellationToken ct = default
	);

	Task ChangeCategoryAsync(
		TransactionCategoryChanged @event,
		CancellationToken ct = default
	);

	Task ChangeDescriptionAsync(
		TransactionDescriptionChanged @event,
		CancellationToken ct = default
	);

	Task IncludeAsync(
		TransactionIncluded @event,
		CancellationToken ct = default
	);

	Task ExcludeAsync(
		TransactionExcluded @event,
		CancellationToken ct = default
	);
}