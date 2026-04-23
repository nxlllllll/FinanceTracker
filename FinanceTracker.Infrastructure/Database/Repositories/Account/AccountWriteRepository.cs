using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Account;

public sealed class AccountWriteRepository(
	FinanceTrackerContext context
) : IAccountWriteRepository
{
    public async Task CreateAsync(
        AccountCreated @event,
        CancellationToken ct = default)
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

	public async Task AdjustBalanceAsync(
		AccountBalanceAdjusted @event,
		CancellationToken ct = default)
	{
		await context.AccountBalances.Where(predicate: balance => balance.AccountId == @event.AccountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(
				propertyExpression: balance => balance.Balance,
				valueExpression: balance => balance.Balance + @event.Delta
			).SetProperty(
				propertyExpression: balance => balance.LastVersion,
				valueExpression: balance => balance.LastVersion + 1
			).SetProperty(
				propertyExpression: e => e.UpdatedAt,
				valueExpression: DateTime.UtcNow
			),
			cancellationToken: ct
		);
	}
	
    public async Task DebitAsync(
        AccountDebited @event,
        CancellationToken ct = default)
    {
        await context.AccountBalances.Where(predicate: b => b.AccountId == @event.AccountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(
				propertyExpression: e => e.Balance,
				valueExpression: e => e.Balance - @event.Amount * @event.ExchangeRate
			).SetProperty(
				propertyExpression: e => e.LastVersion,
				valueExpression: e => e.LastVersion + 1)
			.SetProperty(
				propertyExpression: e => e.UpdatedAt,
				valueExpression: DateTime.UtcNow
			),
			cancellationToken: ct
		);
    }

    public async Task CreditAsync(
        AccountCredited @event,
        CancellationToken ct = default)
    {
        await context.AccountBalances.Where(predicate: b => b.AccountId == @event.AccountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(
				propertyExpression: e => e.Balance,
				valueExpression: e => e.Balance + @event.Amount * @event.ExchangeRate)
			.SetProperty(
				propertyExpression: e => e.LastVersion,
				valueExpression: e => e.LastVersion + 1)
			.SetProperty(
				propertyExpression: e => e.UpdatedAt,
				valueExpression: DateTime.UtcNow
			),
			cancellationToken: ct
		);
    }

	public async Task RenameAsync(
		Guid accountId,
		string newName,
		CancellationToken ct = default)
	{
		await context.Accounts.Where(predicate: a => a.Id == accountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(
				propertyExpression: e => e.Name,
				valueExpression: newName
			), cancellationToken: ct
		);
	}

	public async Task ArchiveAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
		await context.Accounts.Where(predicate: a => a.Id == accountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(
				propertyExpression: e => e.IsArchived,
				valueExpression: true
			), cancellationToken: ct
		);
	}

	public async Task UnarchiveAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
		await context.Accounts.Where(predicate: a => a.Id == accountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(
				propertyExpression: e => e.IsArchived,
				valueExpression: false
			), cancellationToken: ct
		);
	}
}