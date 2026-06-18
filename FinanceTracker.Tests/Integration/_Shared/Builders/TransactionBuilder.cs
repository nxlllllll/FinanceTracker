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
		DirectionType direction = DirectionType.Debit,
		bool isExcluded = false,
		decimal exchangeRate = 1m,
		bool isRatePending = false,
		DateTimeOffset? occurredAt = null)
	{
		Guid transactionId = Guid.CreateVersion7();

		await context.Transactions.AddAsync(new TransactionEntity
		{
			Id = transactionId,
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			Currency = Core.ValueObjects.Currency.Create(value: currencyCode).Value,
			Direction = direction,
			ExchangeRate = exchangeRate,
			IsExcluded = isExcluded,
			IsRatePending = isRatePending,
			Description = null,
			OccurredAt = occurredAt ?? DateTimeOffset.UtcNow
		});

		await context.SaveChangesAsync();
		return transactionId;
	}
}