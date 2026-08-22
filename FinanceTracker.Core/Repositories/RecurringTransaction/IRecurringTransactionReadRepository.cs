using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.RecurringTransaction;

public interface IRecurringTransactionReadRepository : IReadRepository<RecurringTransactionReadModel>
{
	Task<RecurringTransactionReadModel?> GetByIdAsync(
		Guid recurringTransactionId,
		Guid userId,
		CancellationToken ct = default
	);

	Task<PagedResult<RecurringTransactionReadModel>> GetByUserIdAsync(
		Guid userId,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<RecurringTransactionReadModel>> GetDueAsync(
		DateTimeOffset asOf,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<RecurringTransactionReadModel>> GetOverdueAsync(
		DateTimeOffset before,
		CancellationToken ct = default
	);
}
