using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Core.ValueObjects;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZLogger;
using UserRoleAggregate = FinanceTracker.Core.Domains.UserRole.UserRole;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedUserRoleRepository(
	IUserRoleRepository inner,
	IPermissionSourceReadRepository permissionSources,
	RedisCache redisCache,
	IUnitOfWork unitOfWork,
	ILogger<CachedUserRoleRepository> logger
) : IUserRoleRepository
{
	private static readonly DistributedCacheEntryOptions Ttl = new DistributedCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes: 3)
	};

	public Task<UserRoleAggregate?> GetByUserIdAsync(
		Guid userId,
		CancellationToken ct = default
	) => inner.GetByUserIdAsync(userId: userId, ct: ct);

	public async Task SaveAsync(
		UserRoleAggregate userRole,
		CancellationToken ct = default)
	{
		await inner.SaveAsync(userRole: userRole, ct: ct);

		Guid userId = userRole.UserId;
		Guid[] roleIds = [.. userRole.RoleIds];

		IReadOnlySet<string> directGrants = await permissionSources.GetDirectGrantsAsync(userId: userId, ct: ct);
		IReadOnlySet<string> rolePermissions = await permissionSources.GetPermissionsForRolesAsync(roleIds: roleIds, ct: ct);
		IReadOnlySet<SystemRole> heldSystemRoles = await permissionSources.GetSystemRolesAsync(roleIds: roleIds, ct: ct);

		HashSet<string> effective = [.. directGrants, .. rolePermissions];

		unitOfWork.OnCommitted(callback: async () => await RefreshAsync(
			userId: userId,
			effective: effective,
			heldSystemRoles: heldSystemRoles
		));
	}

	private async Task RefreshAsync(
		Guid userId,
		HashSet<string> effective,
		IReadOnlySet<SystemRole> heldSystemRoles)
	{
		bool refreshed = await redisCache.SetAsync(
			key: PermissionCacheKeys.Permissions(userId: userId),
			value: effective,
			options: Ttl
		);

		foreach (SystemRole systemRole in Enum.GetValues<SystemRole>())
		{
			bool written = await redisCache.SetAsync(
				key: PermissionCacheKeys.SystemRoleKey(userId: userId, systemKey: systemRole),
				value: heldSystemRoles.Contains(item: systemRole),
				options: Ttl
			);

			refreshed &= written;
		}

		if (!refreshed)
		{
			logger.ZLogWarning(message: $"""
				Authorization cache for {userId} was not fully refreshed after a membership change;
				the previous entries stay in effect until their TTL expires or the projection worker
				invalidates them.
			""");
		}
	}
}
