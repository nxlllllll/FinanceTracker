using FinanceTracker.Application.Transfers.Commands;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CreateTransferCommandFactory
{
	public static CreateTransferCommand Create(
		Guid? userId = null,
		Guid? fromAccountId = null,
		Guid? toAccountId = null,
		decimal amount = 1000m,
		string? description = "Обед")
	{
		return new CreateTransferCommand(
			UserId: userId ?? Guid.NewGuid(),
			FromAccountId: fromAccountId ?? Guid.NewGuid(),
			ToAccountId: toAccountId ?? Guid.NewGuid(),
			Amount: amount,
			Description: description,
			OccurredAt: DateTime.UtcNow
		);
	}
}