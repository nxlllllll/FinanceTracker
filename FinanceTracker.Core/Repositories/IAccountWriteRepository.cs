using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories;

public interface IAccountWriteRepository
{
	Task CreateAsync(AccountCreated @event, CancellationToken ct = default);
	Task RenameAsync(AccountRenamed @event, CancellationToken ct = default);
	Task ArchiveAsync(AccountArchived @event, CancellationToken ct = default);
	Task UnarchiveAsync(AccountUnarchived @event, CancellationToken ct = default);
}