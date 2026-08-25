using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;
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
		DateTimeOffset? rateStatusChangedAt = null,
		DateTimeOffset? createdAt = null)
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
			Currency = Currency.Create(value: currencyCode).Value,
			BaseCurrency = Currency.Create(value: baseCurrency).Value,
			Direction = direction,
			ExchangeRate = exchangeRate,
			IsExcluded = isExcluded,
			IsCancelled = false,
			CancelledAt = null,
			RateStatus = rateStatus,
			RateStatusChangedAt = rateStatusChangedAt ?? now,
			Description = null,
			CreatedAt = createdAt ?? now,
			OccurredAt = now
		});

		await context.SaveChangesAsync();
		return transactionId;
	}
}
