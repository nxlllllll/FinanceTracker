using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

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
		string? description = "Тест")
	{
		return new CreateTransactionCommand(
			AccountId: accountId ?? Guid.CreateVersion7(),
			UserId: userId ?? Guid.CreateVersion7(),
			CategoryId: categoryId ?? Guid.CreateVersion7(),
			Amount: amount,
			Currency: Currency.Create(value: currency).Value,
			Direction: direction,
			Description: description,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);
	}
}