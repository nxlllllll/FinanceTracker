using FinanceTracker.Application.UseCases.RecurringTransactions.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

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
			UserId: userId ?? Guid.CreateVersion7(),
			AccountId: accountId ?? Guid.CreateVersion7(),
			CategoryId: categoryId ?? Guid.CreateVersion7(),
			Amount: amount,
			Currency: Currency.Create(value: currency).Value,
			Direction: direction,
			DayOfMonth: dayOfMonth,
			Description: description
		);
	}
}
