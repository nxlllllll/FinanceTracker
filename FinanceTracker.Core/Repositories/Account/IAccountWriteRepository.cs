using FinanceTracker.Core.Domains.Account.Events;

namespace FinanceTracker.Core.Repositories.Account;

public interface IAccountWriteRepository
{
	Task CreateAsync(
		AccountCreated @event,
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
	
	Task UpdateBalanceAsync(
		Guid accountId,
		decimal amount,
		CancellationToken ct = default
	);
}