using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionReadRepository
{
	Task<TransactionDto?> GetByIdAsync(
		Guid transactionId,
		CancellationToken ct = default
	);
	
	Task<IReadOnlyList<TransactionDto>> GetAllAsync(
		Guid accountId,
		Guid? categoryId = null,
		DirectionType? direction = null,
		bool? isExcluded = null,
		DateTime? dateFrom = null,
		DateTime? dateTo = null,
		CancellationToken ct = default
	);
	
	Task<bool> ExistsAsync(
		Guid transactionId,
		CancellationToken ct = default
	);
}