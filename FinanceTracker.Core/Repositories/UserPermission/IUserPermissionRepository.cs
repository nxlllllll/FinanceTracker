namespace FinanceTracker.Core.Repositories.UserPermission;

public interface IUserPermissionRepository
{
	Task<Domains.UserPermission.UserPermission?> GetByUserIdAsync(
		Guid userId,
		CancellationToken ct = default
	);

	Task SaveAsync(
		Domains.UserPermission.UserPermission userPermission,
		CancellationToken ct = default
	);
}
