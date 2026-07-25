using FinanceTracker.Core.ReadModels;
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
	public async Task<AccountReadModel?> GetByIdAsync(
		Guid accountId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Accounts.AsNoTracking().Where(predicate: a => a.Id == accountId && a.UserId == userId).Join(
			inner: context.AccountBalances,
			outerKeySelector: a => a.Id,
			innerKeySelector: b => b.AccountId,
			resultSelector: (a, b) => new AccountReadModel(
				Id: a.Id,
				UserId: a.UserId,
				Name: a.Name,
				Type: a.AccountType,
				Balance: Money.Reconstitute(amount: b.Balance, currency: a.Currency),
				IsArchived: a.IsArchived,
				Version: a.LastVersion,
				CreatedAt: a.CreatedAt
			)
		).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<IReadOnlyList<AccountReadModel>> GetAllAsync(
		Guid userId,
		bool? isArchived = null,
		CancellationToken ct = default)
	{
		IQueryable<AccountEntity> accounts = context.Accounts.AsNoTracking().Where(predicate: a => a.UserId == userId);

		if (isArchived is not null)
			accounts = accounts.Where(predicate: a => a.IsArchived == isArchived);

		return await accounts.Join(
			inner: context.AccountBalances,
			outerKeySelector: a => a.Id,
			innerKeySelector: b => b.AccountId,
			resultSelector: (a, b) => new AccountReadModel(
				Id: a.Id,
				UserId: a.UserId,
				Name: a.Name,
				Type: a.AccountType,
				Balance: Money.Reconstitute(amount: b.Balance, currency: a.Currency),
				IsArchived: a.IsArchived,
				Version: a.LastVersion,
				CreatedAt: a.CreatedAt
		)).ToListAsync(cancellationToken: ct);
	}

	public async Task<bool> ExistAsync(
		Guid accountId,
		Guid userId,
		CancellationToken ct = default
	) => await context.Accounts.AsNoTracking().AnyAsync(predicate: a => a.Id == accountId && a.UserId == userId, cancellationToken: ct);
}
