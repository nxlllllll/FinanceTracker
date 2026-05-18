using FinanceTracker.Application.UseCases.Transfers.Commands;
using FinanceTracker.Core.ValueObjects;

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
			UserId: userId ?? Guid.CreateVersion7(),
			FromAccountId: fromAccountId ?? Guid.CreateVersion7(),
			CurrencyFrom: Currency.Create(value: currencyFrom).Value,
			ToAccountId: toAccountId ?? Guid.CreateVersion7(),
			CurrencyTo: Currency.Create(value: currencyTo).Value,
			Amount: amount,
			Description: description,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);
	}
}