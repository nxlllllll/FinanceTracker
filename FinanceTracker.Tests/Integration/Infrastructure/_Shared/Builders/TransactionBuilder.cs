using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

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
		DateTime? occurredAt = null)
	{
		Guid transactionId = Guid.NewGuid();

		await context.Transactions.AddAsync(new TransactionEntity
		{
			Id = transactionId,
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			Currency = Core.ValueObjects.Currency.Create(value: currencyCode).Value,
			Direction = direction,
			ExchangeRate = 1m,
			IsExcluded = isExcluded,
			IsRatePending = false,
			Description = null,
			OccurredAt = occurredAt ?? DateTime.UtcNow
		});

		await context.SaveChangesAsync();
		return transactionId;
	}
}