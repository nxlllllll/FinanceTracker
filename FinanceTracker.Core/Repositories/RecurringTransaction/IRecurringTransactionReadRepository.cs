namespace FinanceTracker.Core.Repositories.RecurringTransaction;

public interface IRecurringTransactionReadRepository
{
	Task<Domains.RecurringTransaction.RecurringTransaction?> GetByIdAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Domains.RecurringTransaction.RecurringTransaction>> GetByUserIdAsync(
		Guid userId,
		DateTime? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default	
	);

	Task<IReadOnlyList<Domains.RecurringTransaction.RecurringTransaction>> GetDueTodayAsync(
		int dayOfMonth,
		int daysInCurrentMonth,
		DateTime currentMonthStart,
		CancellationToken ct = default
	);
}