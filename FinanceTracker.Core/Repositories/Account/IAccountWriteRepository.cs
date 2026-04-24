using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Core.Repositories.Account;

public interface IAccountWriteRepository
{
	Task CreateAsync(
		AccountCreated @event,
		CancellationToken ct = default
	);

	Task AdjustBalanceAsync(
		AccountBalanceAdjusted @event,
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

	Task TransferDebitAsync(
		AccountTransferDebited @event,
		CancellationToken ct = default
	);
 
	Task TransferCreditAsync(
		AccountTransferCredited @event,
		CancellationToken ct = default
	);
	
	Task RenameAsync(
		Guid accountId,
		string newName,
		CancellationToken ct = default
	);

	Task ArchiveAsync(
		Guid accountId,
		CancellationToken ct = default
	);

	Task UnarchiveAsync(
		Guid accountId,
		CancellationToken ct = default
	);
}