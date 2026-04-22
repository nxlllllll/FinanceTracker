namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionRepository
{
	Task<Domains.Transaction.Transaction?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default
	);

	Task SaveAsync(
		Domains.Transaction.Transaction transaction,
		CancellationToken ct = default
	);
}