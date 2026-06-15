using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Repositories.RecurringTransaction;

public interface IRecurringTransactionReadRepository : IReadRepository<RecurringTransactionReadModel>
{
	Task<RecurringTransactionReadModel?> GetByIdAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default
	);

	Task<PagedResult<RecurringTransactionReadModel>> GetByUserIdAsync(
		Guid userId,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default
	);

	IAsyncEnumerable<RecurringTransactionReadModel> GetDueTodayAsync(
		int dayOfMonth,
		int daysInCurrentMonth,
		DateTimeOffset currentMonthStart,
		CancellationToken ct = default
	);
}