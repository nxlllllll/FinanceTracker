namespace FinanceTracker.Core.Repositories.User;

/// <summary>
/// Read access to User for authentication purposes only.
/// Returns the full domain object including PasswordHash.
/// Must not be used in query handlers.
/// </summary>
public interface IUserAuthRepository
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
