using FinanceTracker.Core.ReadModels.Account;

namespace FinanceTracker.Core.Repositories.Account;

public interface IAccountReadRepository : IReadRepository<AccountReadModel>
{
	Task<AccountReadModel?> GetByIdAsync(
		Guid accountId,
		Guid userId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<AccountReadModel>> GetAllAsync(
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
