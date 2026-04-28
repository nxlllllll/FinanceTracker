using FinanceTracker.Application.RecurringTransactions.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CreateRecurringTransactionCommandFactory
{
	public static CreateRecurringTransactionCommand Create(
		Guid? userId = null,
		Guid? accountId = null,
		Guid? categoryId = null,
		decimal amount = 5000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		int dayOfMonth = 15,
		string? description = "Monthly rent")
	{
		return new CreateRecurringTransactionCommand(
			UserId: userId ?? Guid.NewGuid(),
			AccountId: accountId ?? Guid.NewGuid(),
			CategoryId: categoryId ?? Guid.NewGuid(),
			Amount: amount,
			Currency: currency,
			Direction: direction,
			DayOfMonth: dayOfMonth,
			Description: description
		);
	}
}