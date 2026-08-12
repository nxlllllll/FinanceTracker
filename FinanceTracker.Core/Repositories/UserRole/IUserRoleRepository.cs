namespace FinanceTracker.Core.Repositories.UserRole;

public interface IUserRoleRepository
{
	Task<Domains.UserRole.UserRole?> GetByUserIdAsync(
		Guid userId,
		CancellationToken ct = default
	);

	Task SaveAsync(
		Domains.UserRole.UserRole userRole,
		CancellationToken ct = default
	);
}
