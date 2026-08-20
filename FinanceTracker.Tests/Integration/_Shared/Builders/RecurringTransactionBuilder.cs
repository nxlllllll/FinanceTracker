using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;

namespace FinanceTracker.Tests.Integration._Shared.Builders;

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
		string? description = null,
		DateTimeOffset? lastExecutedAt = null,
		DateTimeOffset? lastMissedAt = null,
		DateTimeOffset? nextDueAtUtc = null)
	{
		Guid id = Guid.CreateVersion7();
		DateTimeOffset createdAt = DateTimeOffset.UtcNow;

		await context.RecurringTransactions.AddAsync(new RecurringTransactionEntity()
		{
			Id = id,
			UserId = userId,
			AccountId = accountId,
			CategoryId = categoryId,
			Amount = amount,
			Currency = Currency.Create(value: currency).Value,
			Direction = direction,
			DayOfMonth = dayOfMonth,
			NextDueAtUtc = nextDueAtUtc ?? RecurringDueDate.Next(
				dayOfMonth: dayOfMonth,
				timeZone: TimeZoneId.Utc,
				after: createdAt
			),
			Description = description,
			IsActive = true,
			LastExecutedAt = lastExecutedAt,
			LastMissedAt = lastMissedAt,
			CreatedAt = createdAt
		});

		await context.SaveChangesAsync();
		return id;
	}
}
