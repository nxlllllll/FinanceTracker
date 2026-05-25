using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class TransactionFactory
{
	public static Transaction Create(
		Guid? accountId = null,
		Guid? userId = null,
		Guid? categoryId = null,
		decimal amount = 1000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		decimal exchangeRate = 1m,
		bool isRatePending = false,
		bool isExcluded = false,
		string? description = null)
	{
		Transaction transaction = Transaction.Create(
			occurredAt: FakeDateProvider.Default.UtcNow,
			accountId: accountId ?? Guid.CreateVersion7(),
			userId: userId ?? Guid.CreateVersion7(),
			categoryId: categoryId ?? Guid.CreateVersion7(),
			amount: Money.Create(amount: amount, currency: Currency.Create(value: currency).Value).Value,
			direction: direction,
			exchangeRate: exchangeRate,
			isRatePending: isRatePending,
			description: description
		);

		if (isExcluded)
			transaction.Exclude();

		return transaction;
	}
}
