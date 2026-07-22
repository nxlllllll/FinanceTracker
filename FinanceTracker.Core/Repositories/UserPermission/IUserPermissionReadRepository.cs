namespace FinanceTracker.Core.Repositories.UserPermission;

public interface IUserPermissionReadRepository
{
	Task<IReadOnlySet<string>> GetPermissionsAsync(
		Guid userId,
		CancellationToken ct = default
	);
}
