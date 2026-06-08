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
			currencyFrom: Currency.Reconstitute(value: currencyFrom),
			currencyTo: Currency.Reconstitute(value: currencyTo),
			exchangeRate: exchangeRate,
			isRatePending: isRatePending,
			description: description,
			occurredAt: FakeDateProvider.Default.UtcNow
		).Value!;
	}

	public static Transfer Reconstitute(
		Guid? id = null,
		Guid? userId = null,
		Guid? fromAccountId = null,
		Guid? toAccountId = null,
		decimal amount = 1000m,
		string currencyFrom = "RUB",
		string currencyTo = "RUB",
		decimal exchangeRate = 1m,
		bool isRatePending = false,
		TransferStatus status = TransferStatus.PendingCredit,
		string? description = null)
	{
		Currency from = Currency.Reconstitute(value: currencyFrom);
		Currency to = Currency.Reconstitute(value: currencyTo);

		return Transfer.Reconstitute(
			id: id ?? Guid.CreateVersion7(),
			userId: userId ?? Guid.CreateVersion7(),
			fromAccountId: fromAccountId ?? Guid.CreateVersion7(),
			toAccountId: toAccountId ?? Guid.CreateVersion7(),
			amountFrom: Money.Reconstitute(amount: amount, currency: from),
			amountTo: Money.Reconstitute(amount: amount * exchangeRate, currency: to),
			exchangeRate: exchangeRate,
			isRatePending: isRatePending,
			status: status,
			description: description,
			occurredAt: FakeDateProvider.Default.UtcNow
		);
	}
}