namespace FinanceTracker.Core.Repositories.Account;

public interface IAccountReadRepository
{
	Task<Domains.Account.Account?> GetByIdAsync(
		Guid accountId,
		Guid userId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Domains.Account.Account>> GetAllAsync(
		Guid userId,
		bool? isArchived = null,
		CancellationToken ct = default
	);

	Task<bool> ExistAsync(
		Guid accountId,
		Guid userId,
		CancellationToken ct = default
	);
}