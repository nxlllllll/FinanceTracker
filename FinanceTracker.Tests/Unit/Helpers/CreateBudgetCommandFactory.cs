using FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CreateBudgetCommandFactory
{
	public static CreateBudgetCommand Create(
		Guid? userId = null,
		Guid? categoryId = null,
		string currency = "RUB",
		decimal amount = 50000m,
		DateOnly? from = null,
		DateOnly? to = null)
	{
		DateOnly resolvedFrom = from ?? DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime);

		return new CreateBudgetCommand(
			UserId: userId ?? Guid.CreateVersion7(),
			CategoryId: categoryId ?? Guid.CreateVersion7(),
			Currency: Currency.Create(value: currency).Value,
			Amount: amount,
			From: resolvedFrom,
			To: to ?? resolvedFrom.AddMonths(value: 1)
		);
	}
}