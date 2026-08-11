using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Repositories.UserRole;
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

		IReadOnlySet<string> directGrants = await permissionSources.GetDirectGrantsAsync(userId: userId, ct: ct);
		IReadOnlySet<string> rolePermissions = await permissionSources.GetPermissionsForRolesAsync(
			roleIds: [.. userRole.RoleIds],
			ct: ct
		);

		HashSet<string> effective = [.. directGrants, .. rolePermissions];

		unitOfWork.OnCommitted(callback: async () =>
		{
			bool refreshed = await redisCache.SetAsync(
				key: CachedUserPermissionReadRepository.KeyFor(userId: userId),
				value: effective,
				options: Ttl
			);

			if (!refreshed)
				logger.ZLogWarning(message: $"Permission cache for {userId} was not refreshed; the previous set stays in effect until its TTL expires.");
		});
	}
}
