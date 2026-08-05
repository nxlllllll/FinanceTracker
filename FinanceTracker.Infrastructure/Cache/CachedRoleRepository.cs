using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedRoleRepository(
	IRoleRepository inner,
	RedisCache redisCache,
	IUnitOfWork unitOfWork,
	ILogger<CachedRoleRepository> logger
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

	public async Task ReplacePermissionsAsync(
		Guid roleId,
		IReadOnlySet<Permission> permissions,
		CancellationToken ct = default)
	{
		IReadOnlyList<Guid> memberUserIds = await inner.GetMemberUserIdsAsync(roleId: roleId, ct: ct);

		await inner.ReplacePermissionsAsync(roleId: roleId, permissions: permissions, ct: ct);

		ScheduleInvalidation(userIds: memberUserIds, reason: "role permissions replaced");
	}

	public async Task DeleteAsync(
		Guid roleId,
		CancellationToken ct = default)
	{
		IReadOnlyList<Guid> memberUserIds = await inner.GetMemberUserIdsAsync(roleId: roleId, ct: ct);

		await inner.DeleteAsync(roleId: roleId, ct: ct);

		ScheduleInvalidation(userIds: memberUserIds, reason: "role deleted");
	}

	private void ScheduleInvalidation(IReadOnlyList<Guid> userIds, string reason)
	{
		if (userIds.Count == 0)
			return;

		unitOfWork.OnCommitted(callback: async () =>
		{
			bool invalidated = await InvalidateAsync(userIds: userIds);

			if (!invalidated)
			{
				logger.ZLogWarning(message: $"""
					Permission and role caches for {userIds.Count} member(s) were not invalidated after {reason};
					the previous sets stay in effect until their TTL expires.
				""");
			}
		});
	}

	private async Task<bool> InvalidateAsync(IReadOnlyList<Guid> userIds)
	{
		SystemRole[] systemRoles = Enum.GetValues<SystemRole>();
		List<string> keys = new List<string>(capacity: userIds.Count * (systemRoles.Length + 1));

		foreach (Guid userId in userIds)
		{
			keys.AddRange(collection: systemRoles.Select(systemRole => CachedUserRoleReadRepository.KeyFor(userId: userId, systemKey: systemRole)));
			keys.Add(item: CachedUserPermissionReadRepository.KeyFor(userId: userId));
		}

		return await redisCache.DeleteBatchAsync(keys: keys);
	}
}
