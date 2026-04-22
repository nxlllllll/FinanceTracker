using FinanceTracker.Core.Domains.Transaction;

namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionReadRepository
{
	Task<Domains.Transaction.Transaction?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default
	);
	
	Task<IReadOnlyList<Domains.Transaction.Transaction>> GetAllAsync(
		Guid accountId,
		Guid? categoryId = null,
		DirectionType? direction = null,
		bool? isExcluded = null,
		DateTime? dateFrom = null,
		DateTime? dateTo = null,
		CancellationToken ct = default
	);
}