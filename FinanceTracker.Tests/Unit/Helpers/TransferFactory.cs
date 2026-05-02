using FinanceTracker.Core.Domains.Transfer;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class TransferFactory
{
	public static Transfer Create(
		Guid? userId = null,
		Guid? fromAccountId = null,
		Guid? toAccountId = null,
		decimal amountFrom = 1000m,
		decimal amountTo = 1000m,
		string currencyFrom = "RUB",
		string currencyTo = "RUB",
		decimal exchangeRate = 1m,
		bool isRatePending = false,
		string? description = null)
	{
		return Transfer.Create(
			userId: userId ?? Guid.NewGuid(),
			fromAccountId: fromAccountId ?? Guid.NewGuid(),
			toAccountId: toAccountId ?? Guid.NewGuid(),
			amountFrom: amountFrom,
			currencyFrom: currencyFrom,
			amountTo: amountTo,
			currencyTo: currencyTo,
			exchangeRate: exchangeRate,
			isRatePending: isRatePending,
			description: description,
			occurredAt: FakeDateProvider.Default.UtcNow
		);
	}
}