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
		bool isExcluded = false,
		DirectionType direction = DirectionType.Debit)
	{
		Transaction transaction = Transaction.Create(
			accountId: accountId ?? Guid.NewGuid(),
			userId: userId ?? Guid.NewGuid(),
			categoryId: categoryId ?? Guid.NewGuid(),
			amount: new Money(amount: 1000m, currency: "RUB"),
			direction: direction,
			exchangeRate: 1m,
			isRatePending: false,
			description: null
		);

		if (isExcluded)
			transaction.Exclude();
		
		return transaction;
	}
}