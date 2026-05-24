using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.Repositories.Account;

public sealed class AccountWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider,
	IUnitOfWork unitOfWork,
	ILogger<AccountWriteRepository> logger
) : IAccountWriteRepository
{
	private async Task ApplyBalanceChangeAsync(
		Guid accountId, 
		decimal delta,
		CancellationToken ct)
	{
		await context.AccountBalances.Where(predicate: b => b.AccountId == accountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: e => e.Balance, valueExpression: e => e.Balance + delta)
				.SetProperty(propertyExpression: e => e.LastVersion, valueExpression: e => e.LastVersion + 1)
				.SetProperty(propertyExpression: e => e.UpdatedAt, valueExpression: dateProvider.UtcNow),
			cancellationToken: ct
		);
	}
	
    public async Task CreateAsync(
        AccountCreated @event,
        CancellationToken ct = default)
	{
		await context.Accounts.AddAsync(entity: new AccountEntity()
		{
			Id = @event.AccountId,
			UserId = @event.UserId,
			Name = @event.Name,
			AccountType = @event.Type,
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
			
		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await context.SaveChangesAsync(cancellationToken: ct), 
			onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to create account {@event.AccountId}."),
			ct: ct
		);
	}

	public async Task AdjustBalanceAsync(
		AccountBalanceAdjusted @event,
		CancellationToken ct = default)
	{
		await context.AccountBalances.Where(predicate: balance => balance.AccountId == @event.AccountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: balance => balance.Balance, valueExpression: balance => balance.Balance + @event.Delta)
				.SetProperty(propertyExpression: balance => balance.LastVersion, valueExpression: balance => balance.LastVersion + 1)
				.SetProperty(propertyExpression: e => e.UpdatedAt, valueExpression: dateProvider.UtcNow),
			cancellationToken: ct
		);
	}
	
    public async Task DebitAsync(
        AccountDebited @event,
        CancellationToken ct = default)
    {
		await ApplyBalanceChangeAsync(
			accountId: @event.AccountId,
			delta: -@event.Amount * @event.ExchangeRate,
			ct: ct
		);
    }

    public async Task CreditAsync(
        AccountCredited @event,
        CancellationToken ct = default)
	{
		await ApplyBalanceChangeAsync(
			accountId: @event.AccountId,
			delta: @event.Amount * @event.ExchangeRate,
			ct: ct
		);
	}

	public async Task TransferDebitAsync(
		AccountTransferDebited @event,
		CancellationToken ct = default)
	{
		await ApplyBalanceChangeAsync(
			accountId: @event.AccountId,
			delta: -@event.Amount,
			ct: ct
		);
	}

	public async Task TransferCreditAsync(
		AccountTransferCredited @event, 
		CancellationToken ct = default)
	{
		await ApplyBalanceChangeAsync(
			accountId: @event.AccountId,
			delta: @event.Amount * @event.ExchangeRate,
			ct: ct
		);
	}
	public async Task RefundTransferAsync(
		AccountTransferRefunded @event,
		CancellationToken ct = default)
	{
		await ApplyBalanceChangeAsync(
			accountId: @event.AccountId,
			delta: @event.Amount,
			ct: ct
		);
	}

	public async Task RenameAsync(
		AccountRenamed @event,
		CancellationToken ct = default)
	{
		await context.Accounts.Where(predicate: a => a.Id == @event.AccountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: e => e.Name, valueExpression: @event.NewName),
			cancellationToken: ct
		);
	}

	public async Task ArchiveAsync(
		AccountArchived @event,
		CancellationToken ct = default)
	{
		await context.Accounts.Where(predicate: a => a.Id == @event.AccountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: e => e.IsArchived, valueExpression: true), 
			cancellationToken: ct
		);
	}

	public async Task UnarchiveAsync(
		AccountUnarchived @event,
		CancellationToken ct = default)
	{
		await context.Accounts.Where(predicate: a => a.Id == @event.AccountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: e => e.IsArchived, valueExpression: false), 
			cancellationToken: ct
		);
	}
}