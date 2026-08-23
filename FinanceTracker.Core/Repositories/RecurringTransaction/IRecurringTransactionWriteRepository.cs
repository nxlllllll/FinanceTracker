using FinanceTracker.Core.ValueObjects;

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
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ChangeCurrencyAsync(
		Guid recurringTransactionId,
		ValueObjects.Currency currency,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ChangeDayOfMonthAsync(
		Guid recurringTransactionId,
		int dayOfMonth,
		DateTimeOffset nextDueAtUtc,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ActivateAsync(
		Guid recurringTransactionId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task DeactivateAsync(
		Guid recurringTransactionId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task DeactivateByCategoryIdAsync(
		Guid categoryId,
		CancellationToken ct = default
	);

	Task MarkExecutedAsync(
		Guid recurringTransactionId,
		DateTimeOffset executedAt,
		DateTimeOffset nextDueAtUtc,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task MarkMissedAsync(
		Guid recurringTransactionId,
		DateTimeOffset missedAt,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task RescheduleAllForUserAsync(
		Guid userId,
		TimeZoneId timeZone,
		CancellationToken ct = default
	);
}
