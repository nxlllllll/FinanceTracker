using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Account;

public sealed class AccountReadRepository(
	FinanceTrackerContext context
) : IAccountReadRepository
{
	public async Task<AccountDto?> GetByIdAsync(
		Guid accountId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Accounts.AsNoTracking().Where(predicate: account => account.Id == accountId && account.UserId == userId).Join(
			inner: context.AccountBalances,
			outerKeySelector: account => account.Id,
			innerKeySelector: balance => balance.AccountId,
			resultSelector: (account, balance) => new AccountDto(
				Id: account.Id,
				UserId: account.UserId,
				Name: account.Name,
				Type: account.AccountType,
				Currency: account.Currency,
				Balance: balance.Balance,
				IsArchived: account.IsArchived,
				CreatedAt: account.CreatedAt
			)
		).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<IReadOnlyList<AccountDto>> GetAllAsync(
		Guid userId,
		bool? isArchived = null,
		CancellationToken ct = default)
	{
		IQueryable<AccountEntity> accounts = context.Accounts.AsNoTracking().Where(predicate: account => account.UserId == userId);

		if (isArchived is not null)
			accounts = accounts.Where(predicate: account => account.IsArchived == isArchived);

		return await accounts.Join(
			inner: context.AccountBalances,
			outerKeySelector: account => account.Id,
			innerKeySelector: balance => balance.AccountId,
			resultSelector: (account, balance) => new AccountDto(
				Id: account.Id,
				UserId: account.UserId,
				Name: account.Name,
				Type: account.AccountType,
				Currency: account.Currency,
				Balance: balance.Balance,
				IsArchived: account.IsArchived,
				CreatedAt: account.CreatedAt)
		).ToListAsync(cancellationToken: ct);
	}
}