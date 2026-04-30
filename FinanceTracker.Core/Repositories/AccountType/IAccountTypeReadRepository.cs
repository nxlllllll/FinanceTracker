using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.AccountType;

public interface IAccountTypeReadRepository
{
	Task<IReadOnlyList<AccountTypeDto>> GetAllAsync(CancellationToken ct = default);

	Task<AccountTypeDto?> GetByTypeAsync(
		string type,
		CancellationToken ct = default
	);
}