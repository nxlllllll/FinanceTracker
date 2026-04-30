using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class RecurringTransactionFactory
{
	public static RecurringTransaction Create(
		Guid? userId = null,
		Guid? accountId = null,
		Guid? categoryId = null,
		decimal amount = 5000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		int dayOfMonth = 15,
		string? description = "Monthly rent",
		bool isActive = true)
	{
		RecurringTransaction recurringTransaction = RecurringTransaction.Create(
			userId: userId ?? Guid.NewGuid(),
			accountId: accountId ?? Guid.NewGuid(),
			categoryId: categoryId ?? Guid.NewGuid(),
			amount: new Money(amount: amount, currency: currency),
			direction: direction,
			dayOfMonth: dayOfMonth,
			description: description
		);
		
		if (!isActive)
			recurringTransaction.Deactivate();
		
		return recurringTransaction;
	}
}