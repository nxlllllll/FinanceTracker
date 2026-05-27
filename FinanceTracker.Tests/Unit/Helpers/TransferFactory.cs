using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class TransferFactory
{
	public static Transfer Create(
		Guid? userId = null,
		Guid? fromAccountId = null,
		Guid? toAccountId = null,
		decimal amount = 1000m,
		string currencyFrom = "RUB",
		string currencyTo = "RUB",
		decimal exchangeRate = 1m,
		bool isRatePending = false,
		string? description = null)
	{
		return Transfer.Create(
			userId: userId ?? Guid.CreateVersion7(),
			fromAccountId: fromAccountId ?? Guid.CreateVersion7(),
			toAccountId: toAccountId ?? Guid.CreateVersion7(),
			amount: amount,
			currencyFrom: Currency.Create(value: currencyFrom).Value,
			currencyTo: Currency.Create(value: currencyTo).Value,
			exchangeRate: exchangeRate,
			isRatePending: isRatePending,
			description: description,
			occurredAt: FakeDateProvider.Default.UtcNow
		).Value!;
	}
}