using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Core.Repositories;

public interface IUserRepository
{
	Task<User?> GetByIdAsync(
		Guid userId,
		CancellationToken ct = default
	);
	
	Task<User?> GetByEmailAsync(
		string email,
		CancellationToken ct = default
	);
	
	Task CreateAsync(
		User user,
		CancellationToken ct = default
	);
	
	Task ChangeEmailAsync(
		Guid userId,
		string newEmail,
		CancellationToken ct = default
	);
	
	Task ChangePasswordAsync(
		Guid userId,
		string newPasswordHash,
		CancellationToken ct = default
	);
	
	Task ChangeBaseCurrencyAsync(
		Guid userId,
		string newBaseCurrencyCode,
		CancellationToken ct = default
	);
}