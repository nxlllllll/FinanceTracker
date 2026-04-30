namespace FinanceTracker.Core.Repositories.User;

public interface IUserReadRepository
{
	Task<Domains.User.User?> GetByIdAsync(
		Guid userId,
		CancellationToken ct = default
	);
	
	Task<Domains.User.User?> GetByEmailAsync(
		string email,
		CancellationToken ct = default
	);
}