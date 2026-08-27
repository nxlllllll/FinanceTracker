using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories.Abstractions;

namespace FinanceTracker.Core.Repositories.Account;

public interface IAccountWriteRepository
{
	[EventuallyConsistentCreate]
	Task CreateAsync(
		AccountCreated @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentDelta(ledgerTable: "rm_account_balance_applied_events")]
	Task DebitAsync(
		AccountDebited @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentDelta(ledgerTable: "rm_account_balance_applied_events")]
	Task CreditAsync(
		AccountCredited @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentDelta(ledgerTable: "rm_account_balance_applied_events")]
	Task AdjustBalanceAsync(
		AccountBalanceAdjusted @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentDelta(ledgerTable: "rm_account_balance_applied_events")]
	Task TransferDebitAsync(
		AccountTransferDebited @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentDelta(ledgerTable: "rm_account_balance_applied_events")]
	Task TransferCreditAsync(
		AccountTransferCredited @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentDelta(ledgerTable: "rm_account_balance_applied_events")]
	Task RefundTransferAsync(
		AccountTransferRefunded @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentDelta(ledgerTable: "rm_account_balance_applied_events")]
	Task RevertTransactionAsync(
		AccountTransactionReverted @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentAssignment(versionColumn: "last_version")]
	Task RenameAsync(
		AccountRenamed @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentAssignment(versionColumn: "last_version")]
	Task ArchiveAsync(
		AccountArchived @event,
		CancellationToken ct = default
	);

	[EventuallyConsistentAssignment(versionColumn: "last_version")]
	Task UnarchiveAsync(
		AccountUnarchived @event,
		CancellationToken ct = default
	);

	Task DeleteAsync(
		Guid accountId,
		CancellationToken ct = default
	);

	Task UpsertFromSnapshotAsync(
		Domains.Account.Account account,
		CancellationToken ct = default
	);

	Task<int> DeleteOldBalanceLedgerEntriesAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default
	);
}
