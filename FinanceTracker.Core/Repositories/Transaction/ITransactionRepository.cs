namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionRepository
{
	Task<Domains.Transactions.Transaction?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default
	);

	Task SaveAsync(
		Domains.Transactions.Transaction transaction,
		CancellationToken ct = default
	);
}