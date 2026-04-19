using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Core.Repositories;

public interface IAccountRepository
{
	Task<Account?> GetByIdAsync(Guid accountId, CancellationToken ct = default);
	
	Task SaveAsync(Account account, CancellationToken ct = default);
}