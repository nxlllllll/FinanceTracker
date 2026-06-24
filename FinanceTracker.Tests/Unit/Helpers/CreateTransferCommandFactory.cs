using FinanceTracker.Application.UseCases.Transfer.Commands;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CreateTransferCommandFactory
{
	public static CreateTransferCommand Create(
		Guid? userId = null,
		Guid? fromAccountId = null,
		Guid? toAccountId = null,
		decimal amount = 1000m,
		string? description = "Тест")
	{
		return new CreateTransferCommand(
			UserId: userId ?? Guid.CreateVersion7(),
			FromAccountId: fromAccountId ?? Guid.CreateVersion7(),
			ToAccountId: toAccountId ?? Guid.CreateVersion7(),
			Amount: amount,
			Description: description,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);
	}
}