using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CreateTransactionCommandFactory
{
	public static CreateTransactionCommand Create(
		Guid? accountId = null,
		Guid? userId = null,
		Guid? categoryId = null,
		decimal amount = 1000m,
		DirectionType direction = DirectionType.Debit,
		string? description = "Обед")
	{
		return new CreateTransactionCommand(
			AccountId: accountId ?? Guid.NewGuid(),
			UserId: userId ?? Guid.NewGuid(),
			CategoryId: categoryId ?? Guid.NewGuid(),
			Amount: amount,
			Direction: direction,
			Description: description,
			OccurredAt: DateTime.UtcNow
		);
	}
}