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

	Task<IReadOnlyList<RoleDto>> GetByUserIdAsync(
		Guid userId,
		CancellationToken ct = default
	);

	Task<IReadOnlyList<Guid>> GetMemberUserIdsAsync(
		Guid roleId,
		CancellationToken ct = default
	);

	Task<RoleDto?> GetBySystemKeyAsync(
		string systemKey,
		CancellationToken ct = default
	);

	/// <summary>Guards against removing the last remaining holder of a role with the given system key (e.g. the last root).</summary>
	Task<int> CountMembersWithSystemKeyAsync(
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

	Task DeleteAsync(
		Guid roleId,
		CancellationToken ct = default
	);
}
