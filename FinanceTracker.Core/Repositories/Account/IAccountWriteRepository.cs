using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Core.Repositories.Account;

public interface IAccountWriteRepository
{
	Task CreateAsync(
		AccountCreated @event,
		CancellationToken ct = default
	);

	Task DebitAsync(
		AccountDebited @event,
		CancellationToken ct = default
	);

	Task CreditAsync(
		AccountCredited @event,
		CancellationToken ct = default
	);

	Task AdjustBalanceAsync(
		AccountBalanceAdjusted @event,
		CancellationToken ct = default
	);

	Task TransferDebitAsync(
		AccountTransferDebited @event,
		CancellationToken ct = default
	);

	Task TransferCreditAsync(
		AccountTransferCredited @event,
		CancellationToken ct = default
	);

	Task RefundTransferAsync(
		AccountTransferRefunded @event,
		CancellationToken ct = default
	);

	Task RenameAsync(
		AccountRenamed @event,
		CancellationToken ct = default
	);

	Task ArchiveAsync(
		AccountArchived @event,
		CancellationToken ct = default
	);

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
