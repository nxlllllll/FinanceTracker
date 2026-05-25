using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class AccountBuilder(FinanceTrackerContext context)
{
	public async Task<Guid> CreateAsync(
		Guid userId,
		string currencyCode = "RUB",
		decimal balance = 0)
	{
		Guid accountId = Guid.CreateVersion7();
		await context.Accounts.AddAsync(new AccountEntity()
		{
			Id = accountId,
			UserId = userId,
			Name = Name.Create(value: "Тестовый счёт").Value,
			AccountType = Core.Domains.Account.AccountType.Checking,
			Currency = Core.ValueObjects.Currency.Create(value: currencyCode).Value,
			IsArchived = false,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await context.AccountBalances.AddAsync(new AccountBalanceEntity()
		{
			AccountId = accountId,
			Balance = balance,
			LastVersion = 0,
			UpdatedAt = DateTimeOffset.UtcNow
		});
		await context.SaveChangesAsync();
		return accountId;
	}
}
