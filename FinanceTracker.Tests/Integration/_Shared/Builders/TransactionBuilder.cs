using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transaction;

namespace FinanceTracker.Tests.Integration._Shared.Builders;

public class TransactionBuilder(FinanceTrackerContext context)
{
	public async Task<Guid> CreateAsync(
		Guid userId,
		Guid accountId,
		Guid categoryId,
		decimal amount = 1000m,
		string currencyCode = "RUB",
		string baseCurrency = "RUB",
		DirectionType direction = DirectionType.Debit,
		bool isExcluded = false,
		decimal exchangeRate = 1m,
		RateStatus rateStatus = RateStatus.Exact,
		DateTimeOffset? occurredAt = null,
		DateTimeOffset? rateStatusChangedAt = null)
	{
		Guid transactionId = Guid.CreateVersion7();
		DateTimeOffset now = occurredAt ?? DateTimeOffset.UtcNow;

		await context.Transactions.AddAsync(new TransactionEntity
		{
			Id = transactionId,
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			Currency = Core.ValueObjects.Currency.Create(value: currencyCode).Value,
			BaseCurrency = Core.ValueObjects.Currency.Create(value: baseCurrency).Value,
			Direction = direction,
			ExchangeRate = exchangeRate,
			IsExcluded = isExcluded,
			RateStatus = rateStatus,
			RateStatusChangedAt = rateStatusChangedAt ?? now,
			Description = null,
			OccurredAt = now
		});

		await context.SaveChangesAsync();
		return transactionId;
	}
}
