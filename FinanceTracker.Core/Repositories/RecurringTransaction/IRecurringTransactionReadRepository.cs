using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.RecurringTransaction;

public interface IRecurringTransactionReadRepository
{
	Task<RecurringTransactionDto?> GetByIdAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<RecurringTransactionDto>> GetByUserIdAsync(
		Guid userId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<RecurringTransactionDto>> GetDueTodayAsync(
		int dayOfMonth,
		DateTime currentMonthStart,
		CancellationToken ct = default
	);
}