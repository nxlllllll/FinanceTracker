using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
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
		int rows = await context.AccountBalances.Where(predicate: b => b.AccountId == accountId && b.LastVersion < version).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder.SetProperty(propertyExpression: e => e.Balance, valueExpression: e => e.Balance + delta)
				.SetProperty(propertyExpression: e => e.LastVersion, valueExpression: version)
				.SetProperty(propertyExpression: e => e.UpdatedAt, valueExpression: dateProvider.UtcNow),
			cancellationToken: ct
		);

		if (rows > 0)
			return;

		int? currentVersion = await context.AccountBalances
			.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => (int?)b.LastVersion)
			.FirstOrDefaultAsync(cancellationToken: ct);

		if (currentVersion is null)
			throw new NotFoundException(message: $"Account balance row for {accountId} does not exist yet (AccountCreated not projected?).", id: accountId);

		throw new ConcurrencyConflictException(
			message: $"Account balance {accountId}: cannot apply version {version}, current LastVersion is already {currentVersion}.",
			id: accountId
		);
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

	public async Task DeleteAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
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
