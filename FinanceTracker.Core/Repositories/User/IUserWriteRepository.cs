using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Repositories.User;

public interface IUserWriteRepository
{
	Task CreateAsync(
		Domains.User.User user,
		CancellationToken ct = default
	);
	
	Task ChangeEmailAsync(
		Guid userId,
		Email newEmail,
		CancellationToken ct = default
	);
	
	Task ChangePasswordAsync(
		Guid userId,
		string newPasswordHash,
		CancellationToken ct = default
	);
	
	Task ChangeBaseCurrencyAsync(
		Guid userId,
		ValueObjects.Currency newBaseCurrencyCode,
		CancellationToken ct = default
	);
}