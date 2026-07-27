using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedRoleRepository(
	IRoleRepository inner,
	RedisCache redisCache
) : IRoleRepository
{
	public Task<Guid> CreateAsync(
		Name displayName,
		IReadOnlySet<Permission> permissions,
		DateTimeOffset createdAt,
		CancellationToken ct = default
	) => inner.CreateAsync(displayName: displayName, permissions: permissions, createdAt: createdAt, ct: ct);

	public Task<RoleDto?> GetByIdAsync(
		Guid roleId,
		CancellationToken ct = default
	) => inner.GetByIdAsync(roleId: roleId, ct: ct);

	public Task<IReadOnlyList<RoleDto>> GetAllAsync(
		CancellationToken ct = default
	) => inner.GetAllAsync(ct: ct);

	public Task<IReadOnlyList<RoleDto>> GetByUserIdAsync(
		Guid userId,
		CancellationToken ct = default
	) => inner.GetByUserIdAsync(userId: userId, ct: ct);

	public Task<IReadOnlyList<Guid>> GetMemberUserIdsAsync(
		Guid roleId,
		CancellationToken ct = default
	) => inner.GetMemberUserIdsAsync(roleId: roleId, ct: ct);

	public Task<RoleDto?> GetBySystemKeyAsync(
		SystemRole systemKey,
		CancellationToken ct = default
	) => inner.GetBySystemKeyAsync(systemKey: systemKey, ct: ct);

	public Task<int> CountMembersWithSystemKeyAsync(
		SystemRole systemKey,
		CancellationToken ct = default
	) => inner.CountMembersWithSystemKeyAsync(systemKey: systemKey, ct: ct);

	public Task ReplacePermissionsAsync(
		Guid roleId,
		IReadOnlySet<Permission> permissions,
		CancellationToken ct = default
	) => inner.ReplacePermissionsAsync(roleId: roleId, permissions: permissions, ct: ct);

	public async Task AssignToUserAsync(
		Guid userId,
		Guid roleId,
		DateTimeOffset assignedAt,
		CancellationToken ct = default)
	{
		await inner.AssignToUserAsync(userId: userId, roleId: roleId, assignedAt: assignedAt, ct: ct);
		await InvalidateAsync(userIds: [userId]);
	}

	public async Task RemoveFromUserAsync(
		Guid userId,
		Guid roleId,
		CancellationToken ct = default)
	{
		await inner.RemoveFromUserAsync(userId: userId, roleId: roleId, ct: ct);
		await InvalidateAsync(userIds: [userId]);
	}

	public async Task DeleteAsync(
		Guid roleId,
		CancellationToken ct = default)
	{
		IReadOnlyList<Guid> memberUserIds = await inner.GetMemberUserIdsAsync(roleId: roleId, ct: ct);

		await inner.DeleteAsync(roleId: roleId, ct: ct);
		await InvalidateAsync(userIds: memberUserIds);
	}

	private async Task InvalidateAsync(IReadOnlyList<Guid> userIds)
	{
		if (userIds.Count == 0)
			return;

		SystemRole[] systemRoles = Enum.GetValues<SystemRole>();
		List<string> keys = new List<string>(capacity: userIds.Count * systemRoles.Length);

		foreach (Guid userId in userIds)
			keys.AddRange(collection: systemRoles.Select(systemRole => CachedUserRoleReadRepository.KeyFor(userId: userId, systemKey: systemRole)));

		await redisCache.DeleteBatchAsync(keys: keys);
	}
}
