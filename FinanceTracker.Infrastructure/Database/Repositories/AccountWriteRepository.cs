using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace FinanceTracker.Infrastructure.Database.Repositories;

public sealed class AccountWriteRepository(
	FinanceTrackerContext context
) : IAccountWriteRepository
{
	private async Task ChangeAccountProperty(
		Guid accountId, 
		Action<UpdateSettersBuilder<AccountEntity>> changePropertyAction,
		CancellationToken ct = default)
	{
		await context.Accounts.Where(predicate: account => account.Id == accountId).ExecuteUpdateAsync(
			setPropertyCalls: changePropertyAction,
			cancellationToken: ct
		);
	}
	
	public async Task CreateAsync(AccountCreated @event, CancellationToken ct = default)
	{
		await context.Accounts.AddAsync(entity: new AccountEntity()
		{
			Id = @event.AccountId,
			UserId = @event.UserId,
			Name = @event.Name,
			AccountType = @event.AccountType,
			Currency = @event.Currency,
			IsArchived = false,
			CreatedAt = @event.OccurredAt
		}, cancellationToken: ct);

		await context.AccountBalances.AddAsync(entity: new AccountBalanceEntity()
		{
			AccountId = @event.AccountId,
			Balance = @event.Balance,
			LastVersion = 1,
			UpdatedAt = @event.OccurredAt
		}, cancellationToken: ct);
		
		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task RenameAsync(AccountRenamed @event, CancellationToken ct = default)
	{
		await ChangeAccountProperty(
			accountId: @event.AccountId,
			changePropertyAction: builder => builder.SetProperty(propertyExpression: entity => entity.Name, valueExpression: @event.NewName),
			ct: ct
		);
	}

	public async Task ArchiveAsync(AccountArchived @event, CancellationToken ct = default)
	{
		await ChangeAccountProperty(
			accountId: @event.AccountId,
			changePropertyAction: builder => builder.SetProperty(propertyExpression: entity => entity.IsArchived, valueExpression: true), 
			ct: ct
		);
	}

	public async Task UnarchiveAsync(AccountUnarchived @event, CancellationToken ct = default)
	{
		await ChangeAccountProperty(
			accountId: @event.AccountId,
			changePropertyAction: builder => builder.SetProperty(propertyExpression: entity => entity.IsArchived, valueExpression: false), 
			ct: ct
		);
	}
}