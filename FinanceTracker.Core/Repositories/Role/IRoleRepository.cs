using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Repositories.Role;

public interface IRoleRepository
{
	Task<Guid> CreateAsync(
		Name displayName,
		IReadOnlySet<Permission> permissions,
		DateTimeOffset createdAt,
		CancellationToken ct = default
	);

	Task<RoleDto?> GetByIdAsync(
		Guid roleId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<RoleDto>> GetAllAsync(
		CancellationToken ct = default
	);

	Task<RoleDto?> GetBySystemKeyAsync(
		string systemKey,
		CancellationToken ct = default
	);

	Task ReplacePermissionsAsync(
		Guid roleId,
		IReadOnlySet<Permission> permissions,
		CancellationToken ct = default
	);

	Task AssignToUserAsync(
		Guid userId,
		Guid roleId,
		DateTimeOffset assignedAt,
		CancellationToken ct = default
	);

	Task RemoveFromUserAsync(
		Guid userId,
		Guid roleId,
		CancellationToken ct = default
	);

	/// <summary>User ids currently holding this role — used to fan out Grant/Revoke when the role's permission set changes.</summary>
	Task<IReadOnlyList<Guid>> GetMemberUserIdsAsync(
		Guid roleId,
		CancellationToken ct = default
	);

	/// <summary>Guards against removing the last remaining holder of a role with the given system key (e.g. the last root).</summary>
	Task<int> CountMembersWithSystemKeyAsync(
		string systemKey,
		CancellationToken ct = default
	);
}
