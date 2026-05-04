using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class AccountBuilder(FinanceTrackerContext context)
{
	private readonly AccountTypeBuilder _accountTypeBuilder = new AccountTypeBuilder(context: context);
	
	public async Task<Guid> CreateAsync(
		Guid userId,
		string currencyCode = "RUB",
		decimal balance = 0)
	{
		await _accountTypeBuilder.CreateAsync();

		Guid accountId = Guid.NewGuid();
		await context.Accounts.AddAsync(new AccountEntity()
		{
			Id = accountId,
			UserId = userId,
			Name = "Тестовый счёт",
			AccountType = Core.Domains.Account.AccountType.Checking,
			Currency = currencyCode,
			IsArchived = false,
			CreatedAt = DateTime.UtcNow
		});
		await context.AccountBalances.AddAsync(new AccountBalanceEntity()
		{
			AccountId = accountId,
			Balance = balance,
			LastVersion = 0,
			UpdatedAt = DateTime.UtcNow
		});
		await context.SaveChangesAsync();
		return accountId;
	}
}