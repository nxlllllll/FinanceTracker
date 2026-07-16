namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionRepository
{
	Task<Domains.Transaction.Transaction?> GetByIdAsync(
		Guid transactionId,
		Guid userId,
		CancellationToken ct = default
	);
}
