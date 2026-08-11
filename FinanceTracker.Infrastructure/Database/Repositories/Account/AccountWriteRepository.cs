using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
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
		DateTimeOffset now = dateProvider.UtcNow;

		bool balanceRowExists = await context.AccountBalances.AnyAsync(predicate: b => b.AccountId == accountId, cancellationToken: ct);
		if (!balanceRowExists)
			throw new NotFoundException(message: $"Account balance row for {accountId} does not exist yet (AccountCreated not projected?).", id: accountId);

		bool isNewlyApplied = await context.TryRecordAccountBalanceEventAppliedAsync(
			accountId: accountId,
			version: version,
			appliedAt: now,
			ct: ct
		);

		if (!isNewlyApplied)
			return;

		int rows = await context.AccountBalances.Where(predicate: b => b.AccountId == accountId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: e => e.Balance, valueExpression: e => e.Balance + delta)
				.SetProperty(propertyExpression: e => e.LastVersion, valueExpression: e => e.LastVersion > version ? e.LastVersion : version)
				.SetProperty(propertyExpression: e => e.UpdatedAt, valueExpression: now),
			cancellationToken: ct
		);

		if (rows == 0)
			throw new NotFoundException(message: $"Account balance row for {accountId} does not exist yet (AccountCreated not projected?).", id: accountId);
	}

	private async Task EnsureAccountExistsAsync(Guid accountId, CancellationToken ct)
	{
		bool accountExists = await context.Accounts.AnyAsync(predicate: a => a.Id == accountId, cancellationToken: ct);
		if (!accountExists)
			throw new NotFoundException(message: $"Account {accountId} does not exist yet (AccountCreated not projected?).", id: accountId);
	}

	public async Task<int> DeleteOldBalanceLedgerEntriesAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.AccountBalanceAppliedEvents.Where(predicate: e => e.AppliedAt < before)
			.OrderBy(keySelector: e => e.AppliedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}

	public async Task CreateAsync(
		AccountCreated @event,
		CancellationToken ct = default)
	{
		await context.InsertAccountAsync(
			id: @event.AccountId,
			userId: @event.UserId,
			name: @event.Name.Value,
			accountTypeCode: @event.Type.ToString().ToLowerInvariant(),
			currencyCode: @event.Currency.Value,
			isArchived: false,
			lastVersion: @event.Version,
			createdAt: @event.OccurredAt,
			ct: ct
		);

		await context.InsertAccountBalanceAsync(
			accountId: @event.AccountId,
			balance: @event.Balance,
			lastVersion: @event.Version,
			updatedAt: @event.OccurredAt,
			ct: ct
		);
	}

	public async Task AdjustBalanceAsync(
		AccountBalanceAdjusted @event,
		CancellationToken ct = default)
	{
		await ApplyBalanceChangeAsync(
			accountId: @event.AccountId,
			delta: @event.Delta,
			version: @event.Version,
			ct: ct
		);
	}

	public async Task DebitAsync(
		AccountDebited @event,
		CancellationToken ct = default)
	{
		await ApplyBalanceChangeAsync(
			accountId: @event.AccountId,
			delta: -Money.ConvertedAmount(amount: @event.Amount, rate: @event.ExchangeRate),
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
			delta: Money.ConvertedAmount(amount: @event.Amount, rate: @event.ExchangeRate),
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
			delta: Money.ConvertedAmount(amount: @event.Amount, rate: @event.ExchangeRate),
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
		await EnsureAccountExistsAsync(accountId: @event.AccountId, ct: ct);

		await context.Accounts.Where(predicate: a => a.Id == @event.AccountId && a.LastVersion < @event.Version).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: e => e.Name, valueExpression: @event.NewName)
				.SetProperty(propertyExpression: e => e.LastVersion, valueExpression: @event.Version),
			cancellationToken: ct
		);
	}

	public async Task ArchiveAsync(
		AccountArchived @event,
		CancellationToken ct = default)
	{
		await EnsureAccountExistsAsync(accountId: @event.AccountId, ct: ct);

		await context.Accounts.Where(predicate: a => a.Id == @event.AccountId && a.LastVersion < @event.Version).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: e => e.IsArchived, valueExpression: true)
				.SetProperty(propertyExpression: e => e.LastVersion, valueExpression: @event.Version),
			cancellationToken: ct
		);
	}

	public async Task UnarchiveAsync(
		AccountUnarchived @event,
		CancellationToken ct = default)
	{
		await EnsureAccountExistsAsync(accountId: @event.AccountId, ct: ct);

		await context.Accounts.Where(predicate: a => a.Id == @event.AccountId && a.LastVersion < @event.Version).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: e => e.IsArchived, valueExpression: false)
				.SetProperty(propertyExpression: e => e.LastVersion, valueExpression: @event.Version),
			cancellationToken: ct
		);
	}

	public async Task DeleteAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
		await context.AccountBalanceAppliedEvents.Where(predicate: e => e.AccountId == accountId).ExecuteDeleteAsync(cancellationToken: ct);
		await context.AccountBalances.Where(predicate: b => b.AccountId == accountId).ExecuteDeleteAsync(cancellationToken: ct);
		await context.Accounts.Where(predicate: a => a.Id == accountId).ExecuteDeleteAsync(cancellationToken: ct);
	}

	public async Task UpsertFromSnapshotAsync(
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		await DeleteAsync(accountId: account.Id, ct: ct);

		await context.InsertAccountAsync(
			id: account.Id,
			userId: account.UserId,
			name: account.Name.Value,
			accountTypeCode: account.Type.ToString().ToLowerInvariant(),
			currencyCode: account.Currency.Value,
			isArchived: account.IsArchived,
			lastVersion: account.Version,
			createdAt: account.CreatedAt,
			ct: ct
		);

		await context.InsertAccountBalanceAsync(
			accountId: account.Id,
			balance: account.Balance.Amount,
			lastVersion: account.Version,
			updatedAt: dateProvider.UtcNow,
			ct: ct
		);
	}
}
