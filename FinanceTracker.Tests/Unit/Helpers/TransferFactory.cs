using FinanceTracker.Core.Domains.Abstractions.Rate;
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
		RateStatus rateStatus = RateStatus.Exact,
		string? description = null,
		DateTimeOffset? occurredAt = null,
		DateTimeOffset? createdAt = null)
	{
		return Transfer.Create(
			createdAt: createdAt ?? FakeDateProvider.Default.UtcNow,
			userId: userId ?? Guid.CreateVersion7(),
			fromAccountId: fromAccountId ?? Guid.CreateVersion7(),
			toAccountId: toAccountId ?? Guid.CreateVersion7(),
			amount: amount,
			currencyFrom: Currency.Reconstitute(value: currencyFrom),
			currencyTo: Currency.Reconstitute(value: currencyTo),
			exchangeRate: exchangeRate,
			rateStatus: rateStatus,
			description: description,
			occurredAt: occurredAt ?? FakeDateProvider.Default.UtcNow
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
		RateStatus rateStatus = RateStatus.Exact,
		DateTimeOffset? rateStatusChangedAt = null,
		TransferStatus status = TransferStatus.PendingCredit,
		int rowVersion = 0,
		string? description = null,
		DateTimeOffset? occurredAt = null,
		DateTimeOffset? createdAt = null)
	{
		Currency from = Currency.Reconstitute(value: currencyFrom);
		Currency to = Currency.Reconstitute(value: currencyTo);

		return Transfer.Reconstitute(
			id: id ?? Guid.CreateVersion7(),
			userId: userId ?? Guid.CreateVersion7(),
			fromAccountId: fromAccountId ?? Guid.CreateVersion7(),
			toAccountId: toAccountId ?? Guid.CreateVersion7(),
			amountFrom: Money.Reconstitute(amount: amount, currency: from),
			amountTo: Money.Reconstitute(amount: Money.ConvertedAmount(amount: amount, rate: exchangeRate), currency: to),
			exchangeRate: exchangeRate,
			rateStatus: rateStatus,
			rateStatusChangedAt: rateStatusChangedAt ?? FakeDateProvider.Default.UtcNow,
			status: status,
			description: description,
			rowVersion: rowVersion,
			occurredAt: occurredAt ?? FakeDateProvider.Default.UtcNow,
			createdAt: createdAt ?? FakeDateProvider.Default.UtcNow
		);
	}
}
