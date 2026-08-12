namespace FinanceTracker.Core.Repositories.UserPermission;

/// <summary>
/// Reads the two independent sources a user's effective permissions are assembled from: permissions
/// granted to them directly, and permissions carried by the roles they belong to.
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
}
