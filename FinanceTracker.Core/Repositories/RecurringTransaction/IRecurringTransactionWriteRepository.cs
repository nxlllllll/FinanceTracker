using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Repositories.RecurringTransaction;

public interface IRecurringTransactionWriteRepository
{
	Task CreateAsync(
		Domains.RecurringTransaction.RecurringTransaction recurringTransaction,
		CancellationToken ct = default
	);

	Task ChangeAmountAsync(
		Guid recurringTransactionId,
		decimal amount,
		CancellationToken ct = default
	);
	
	Task ChangeCurrencyAsync(
		Guid recurringTransactionId,
		string currency,
		CancellationToken ct = default
	);

	Task ChangeDayOfMonthAsync(
		Guid recurringTransactionId,
		int dayOfMonth,
		CancellationToken ct = default
	);

	Task ActivateAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default
	);

	Task DeactivateAsync(
		Guid recurringTransactionId,
		CancellationToken ct = default
	);

	Task DeactivateByCategoryIdAsync(
		Guid categoryId,
		CancellationToken ct = default
	);
	
	Task MarkExecutedAsync(
		Guid recurringTransactionId,
		DateTime executedAt,
		CancellationToken ct = default
	);
}