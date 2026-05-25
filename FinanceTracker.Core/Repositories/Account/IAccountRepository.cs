namespace FinanceTracker.Core.Repositories.Account;

public interface IAccountRepository
{
	Task<Domains.Account.Account?> GetByIdAsync(Guid accountId, CancellationToken ct = default);

	Task SaveAsync(Domains.Account.Account account, CancellationToken ct = default);
}
