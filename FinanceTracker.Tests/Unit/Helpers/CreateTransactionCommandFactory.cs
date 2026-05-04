using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CreateTransactionCommandFactory
{
	public static CreateTransactionCommand Create(
		Guid? accountId = null,
		Guid? userId = null,
		Guid? categoryId = null,
		decimal amount = 1000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		string? description = "Обед")
	{
		return new CreateTransactionCommand(
			AccountId: accountId ?? Guid.NewGuid(),
			UserId: userId ?? Guid.NewGuid(),
			CategoryId: categoryId ?? Guid.NewGuid(),
			Amount: amount,
			Currency: currency,
			Direction: direction,
			Description: description,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);
	}
}