using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.Transaction;

public interface ITransactionReadRepository : IReadRepository<TransactionReadModel>
{
	Task<TransactionReadModel?> GetByIdAsync(
		Guid transactionId,
		Guid userId,
		CancellationToken ct = default
	);

	Task<PagedResult<TransactionReadModel>> GetAllAsync(
		Guid userId,
		Guid accountId,
		Guid? categoryId = null,
		DirectionType? direction = null,
		bool? isExcluded = null,
		DateTimeOffset? dateFrom = null,
		DateTimeOffset? dateTo = null,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<PendingRateTransaction>> GetPendingRateAsync(
		int batchSize,
		DateTimeOffset? cursorOccurredAt = null,
		Guid? cursorId = null,
		CancellationToken ct = default
	);
}
