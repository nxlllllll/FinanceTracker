using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Account;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Account;

public sealed class AccountReadRepository(
	FinanceTrackerContext context
) : IAccountReadRepository
{
	public async Task<Core.Domains.Account.Account?> GetByIdAsync(
		Guid accountId,
		Guid userId,
		CancellationToken ct = default)
	{
		Core.Domains.Account.Account? raw = await context.Accounts.AsNoTracking().Where(predicate: account => account.Id == accountId && account.UserId == userId).Join(
			inner: context.AccountBalances,
			outerKeySelector: account => account.Id,
			innerKeySelector: balance => balance.AccountId,
			resultSelector: (account, balance) => Core.Domains.Account.Account.Reconstitute(
				id: account.Id,
				userId: account.UserId,
				name: account.Name,
				type: account.AccountType,
				balance: Money.Reconstitute(amount: balance.Balance, currency: account.Currency),
				isArchived: account.IsArchived,
				createdAt: account.CreatedAt
			)
		).FirstOrDefaultAsync(cancellationToken: ct);

		return raw;
	}

	public async Task<IReadOnlyList<Core.Domains.Account.Account>> GetAllAsync(
		Guid userId,
		bool? isArchived = null,
		CancellationToken ct = default)
	{
		IQueryable<AccountEntity> accounts = context.Accounts.AsNoTracking().Where(predicate: account => account.UserId == userId);

		if (isArchived is not null)
			accounts = accounts.Where(predicate: account => account.IsArchived == isArchived);

		List<Core.Domains.Account.Account> result = await accounts.Join(
			inner: context.AccountBalances,
			outerKeySelector: account => account.Id,
			innerKeySelector: balance => balance.AccountId,
			resultSelector: (account, balance) => Core.Domains.Account.Account.Reconstitute(
				id: account.Id,
				userId: account.UserId,
				name: account.Name,
				type: account.AccountType,
				balance: Money.Reconstitute(amount: balance.Balance, currency: account.Currency),
				isArchived: account.IsArchived,
				createdAt: account.CreatedAt
		)).ToListAsync(cancellationToken: ct);

		return result.AsReadOnly();
	}

	public async Task<bool> ExistAsync(
		Guid accountId,
		Guid userId,
		CancellationToken ct = default
	) => await context.Accounts.AsNoTracking().AnyAsync(predicate: account => account.Id == accountId && account.UserId == userId, cancellationToken: ct);
}