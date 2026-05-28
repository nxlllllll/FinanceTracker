namespace FinanceTracker.Core.Repositories.RecurringTransaction;

public interface IRecurringTransactionRepository
{
	Task<Domains.RecurringTransaction.RecurringTransaction?> GetByIdAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default
	);
}