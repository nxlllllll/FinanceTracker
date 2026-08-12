using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class TransactionFactory
{
	public static Transaction Create(
		Guid? accountId = null,
		Guid? userId = null,
		Guid? categoryId = null,
		decimal amount = 1000m,
		string currency = "RUB",
		string baseCurrency = "RUB",
		DirectionType direction = DirectionType.Debit,
		decimal exchangeRate = 1m,
		RateStatus rateStatus = RateStatus.Pending,
		bool isExcluded = false,
		string? description = null)
	{
		Result<Transaction, DomainException> result = Transaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			occurredAt: FakeDateProvider.Default.UtcNow,
			accountId: accountId ?? Guid.CreateVersion7(),
			userId: userId ?? Guid.CreateVersion7(),
			categoryId: categoryId ?? Guid.CreateVersion7(),
			amount: Money.Create(amount: amount, currency: Currency.Create(value: currency).Value).Value,
			baseCurrency: Currency.Create(value: baseCurrency).Value,
			direction: direction,
			exchangeRate: exchangeRate,
			rateStatus: rateStatus,
			description: description
		);

		if (result.IsFailure)
			throw result.Error!;

		Transaction transaction = result.Value!;

		if (isExcluded)
			transaction.Exclude();

		return transaction;
	}

	public static TransactionReadModel CreateReadModel(
		Guid? accountId = null,
		Guid? userId = null,
		Guid? categoryId = null,
		decimal amount = 1000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		decimal exchangeRate = 1m,
		RateStatus rateStatus = RateStatus.Pending,
		bool isExcluded = false,
		string? description = null)
	{
		return new TransactionReadModel(
			Id: Guid.CreateVersion7(),
			AccountId: accountId ?? Guid.CreateVersion7(),
			UserId: userId ?? Guid.CreateVersion7(),
			CategoryId: categoryId ?? Guid.CreateVersion7(),
			Amount: Money.Create(amount: amount, currency: Currency.Create(value: currency).Value).Value,
			Direction: direction,
			ExchangeRate: exchangeRate,
			IsExcluded: isExcluded,
			RateStatus: rateStatus,
			Description: description,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);
	}
}
