using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Repositories.UserPermission;

/// <summary>
/// Reads the sources a user's authorization state is assembled from: permissions granted to them
/// directly, permissions carried by the roles they belong to, and which of those roles are system
/// roles. All three feed the same Redis entries (see <c>PermissionCacheKeys</c>), so they are read
/// together whenever that state has to be recomputed.
/// </summary>
public interface IPermissionSourceReadRepository
{
	/// <summary>Permissions granted to the user personally.</summary>
	Task<IReadOnlySet<string>> GetDirectGrantsAsync(
		Guid userId,
		CancellationToken ct = default
	);

	/// <summary>
	/// Permissions the user holds through the roles they currently belong to.
	/// </summary>
	Task<IReadOnlySet<string>> GetRoleGrantsAsync(
		Guid userId,
		CancellationToken ct = default
	);

	/// <summary>
	/// Permissions carried by the given roles, regardless of who belongs to them. Used when membership
	/// has just changed and the stored one is therefore not the membership we mean.
	/// </summary>
	Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(
		IReadOnlyCollection<Guid> roleIds,
		CancellationToken ct = default
	);

	/// <summary>
	/// Which system roles are among <paramref name="roleIds"/>. Custom roles carry no system key and
	/// are simply absent from the result.
	/// </summary>
	Task<IReadOnlySet<SystemRole>> GetSystemRolesAsync(
		IReadOnlyCollection<Guid> roleIds,
		CancellationToken ct = default
	);
}
