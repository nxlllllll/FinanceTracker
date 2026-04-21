using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories;

public interface IAccountTypeRepository
{
	Task<IReadOnlyList<AccountTypeDto>> GetAllAsync(CancellationToken ct = default);

	Task<AccountTypeDto?> GetByTypeAsync(
		string type,
		CancellationToken ct = default
	);
}