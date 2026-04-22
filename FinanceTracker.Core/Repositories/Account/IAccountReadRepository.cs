using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.Account;

public interface IAccountReadRepository
{
	Task<AccountDto?> GetByIdAsync(
		Guid accountId,
		CancellationToken ct = default
	);
	
	Task<IReadOnlyList<AccountDto>> GetAllAsync(
		Guid userId,
		bool? isArchived = null,
		CancellationToken ct = default
	);
}