using FinanceTracker.Application.Transfers.Commands;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CreateTransferCommandFactory
{
	public static CreateTransferCommand Create(
		Guid? userId = null,
		Guid? fromAccountId = null,
		string currencyFrom = "RUB",
		Guid? toAccountId = null,
		string currencyTo = "RUB",
		decimal amount = 1000m,
		string? description = "Обед")
	{
		return new CreateTransferCommand(
			UserId: userId ?? Guid.NewGuid(),
			FromAccountId: fromAccountId ?? Guid.NewGuid(),
			CurrencyFrom: currencyFrom,
			ToAccountId: toAccountId ?? Guid.NewGuid(),
			CurrencyTo: currencyTo,
			Amount: amount,
			Description: description,
			OccurredAt: DateTime.UtcNow
		);
	}
}