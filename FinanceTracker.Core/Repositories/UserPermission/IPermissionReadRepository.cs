namespace FinanceTracker.Core.Repositories.UserPermission;

public interface IPermissionReadRepository
{
	Task<IReadOnlySet<string>> GetPermissionsAsync(
		Guid userId,
		CancellationToken ct = default
	);
}
