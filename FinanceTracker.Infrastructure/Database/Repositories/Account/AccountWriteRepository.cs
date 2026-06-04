using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Account;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Account;

public sealed class AccountWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : IAccountWriteRepository
{
	private async Task ApplyBalanceChangeAsync(
		Guid accountId,
		decimal delta,
		int version,
		CancellationToken ct)
	{
		int rows = await context.AccountBalances.Where(predicate: b => b.AccountId == accountId && b.LastVersion < version).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: e => e.Balance, valueExpression: e => e.Balance + delta)
				.SetProperty(propertyExpression: e => e.LastVersion, valueExpression: version)
				.SetProperty(propertyExpression: e => e.UpdatedAt, valueExpression: dateProvider.UtcNow),
			cancellationToken: ct
		);

		if (rows == 0)
			throw new ConcurrencyConflictException(message: $"Concurrency conflict: account balance {accountId} was already updated to version >= {version}.", id: accountId);
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
			LastVersion = @event.Version,
			UpdatedAt = @event.OccurredAt
		}, cancellationToken: ct);
	}

	public async Task AdjustBalanceAsync(
		AccountBalanceAdjusted @event,
		CancellationToken ct = default)
	{
		int rows = await context.AccountBalances.Where(predicate: balance => balance.AccountId == @event.AccountId && balance.LastVersion < @event.Version).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: balance => balance.Balance, valueExpression: balance => balance.Balance + @event.Delta)
				.SetProperty(propertyExpression: balance => balance.LastVersion, valueExpression: @event.Version)
				.SetProperty(propertyExpression: e => e.UpdatedAt, valueExpression: dateProvider.UtcNow),
			cancellationToken: ct
		);

		if (rows == 0)
		{
			throw new ConcurrencyConflictException(
				message:
				$"Concurrency conflict: account balance {@event.AccountId} was already updated to version >= {@event.Version}.",
				id: @event.AccountId
			);
		}
	}

	public async Task DebitAsync(
		AccountDebited @event,
		CancellationToken ct = default)
	{
		await ApplyBalanceChangeAsync(
			accountId: @event.AccountId,
			delta: -@event.Amount * @event.ExchangeRate,
			version: @event.Version,
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
			version: @event.Version,
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
			version: @event.Version,
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
			version: @event.Version,
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
			version: @event.Version,
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