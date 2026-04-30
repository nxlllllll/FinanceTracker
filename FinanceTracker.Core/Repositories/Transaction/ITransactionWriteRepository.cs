using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionWriteRepository
{
	Task CreateAsync(
		Domains.Transaction.Transaction transaction,
        CancellationToken ct = default
	);

	Task ChangeCategoryAsync(
		Guid transactionId,
		Guid categoryId,
		CancellationToken ct = default
	);

	Task ChangeDescriptionAsync(
		Guid transactionId,
		string? description,
		CancellationToken ct = default
	);

	Task IncludeAsync(
		Guid transactionId,
		CancellationToken ct = default
	);

	Task ExcludeAsync(
		Guid transactionId,
		CancellationToken ct = default
	);
}