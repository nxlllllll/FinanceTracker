using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories;

public sealed class AccountReadRepository(
	FinanceTrackerContext context
) : IAccountReadRepository
{
	public async Task<AccountDto?> GetByIdAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
		return await context.Accounts.AsNoTracking().Where(predicate: account => account.Id == accountId).Join(
			inner: context.AccountBalances,
			outerKeySelector: account => account.Id,
			innerKeySelector: balance => balance.AccountId,
			resultSelector: (account, balance) => new AccountDto(
				Id: account.Id,
				UserId: account.UserId,
				Name: account.Name,
				AccountType: account.AccountType,
				Currency: account.Currency,
				Balance: balance.Balance,
				IsArchived: account.IsArchived,
				CreatedAt: account.CreatedAt
			)
		).FirstOrDefaultAsync(cancellationToken: ct);
	}
}