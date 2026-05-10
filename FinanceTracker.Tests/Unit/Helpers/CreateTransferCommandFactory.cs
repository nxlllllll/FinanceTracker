using FinanceTracker.Application.UseCases.Transfers.Commands;

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
			CurrencyFrom: currencyFrom,
			ToAccountId: toAccountId ?? Guid.CreateVersion7(),
			CurrencyTo: currencyTo,
			Amount: amount,
			Description: description,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);
	}
}