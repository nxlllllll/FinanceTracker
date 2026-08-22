using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Account;

namespace FinanceTracker.Tests.Integration._Shared.Builders;

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
			Name = Name.Create(value: "Основной счёт").Value,
			AccountType = AccountType.Checking,
			Currency = Currency.Create(value: currencyCode).Value,
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
