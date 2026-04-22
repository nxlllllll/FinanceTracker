using FinanceTracker.Core.Domains.Transactions;

namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionReadRepository
{
	Task<Domains.Transactions.Transaction?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default
	);
	
	Task<IReadOnlyList<Domains.Transactions.Transaction>> GetAllAsync(
		Guid accountId,
		Guid? categoryId = null,
		DirectionType? direction = null,
		bool? isExcluded = null,
		DateTime? dateFrom = null,
		DateTime? dateTo = null,
		CancellationToken ct = default
	);
}