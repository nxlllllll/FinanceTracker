namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionReadRepository
{
	Task<Domains.Transactions.Transaction?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default
	);
}