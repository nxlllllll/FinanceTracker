using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public sealed class RecurringTransactionBuilder(FinanceTrackerContext context)
{
	public async Task<Guid> CreateAsync(
		Guid userId,
		Guid accountId,
		Guid categoryId,
		decimal amount = 5000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		int dayOfMonth = 15,
		string? description = null)
	{
		Guid id = Guid.NewGuid();

		await context.RecurringTransactions.AddAsync(new RecurringTransactionEntity()
		{
			Id = id,
			UserId = userId,
			AccountId = accountId,
			CategoryId = categoryId,
			Amount = amount,
			Currency = Core.ValueObjects.Currency.Create(value: currency).Value,
			Direction = direction,
			DayOfMonth = dayOfMonth,
			Description = description,
			IsActive = true,
			LastExecutedAt = null,
			CreatedAt = DateTime.UtcNow
		});

		await context.SaveChangesAsync();
		return id;
	}
}