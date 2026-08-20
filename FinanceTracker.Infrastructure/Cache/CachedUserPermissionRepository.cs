using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ZLogger;
using UserPermissionAggregate = FinanceTracker.Core.Domains.UserPermission.UserPermission;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedUserPermissionRepository(
	IUserPermissionRepository inner,
	IPermissionSourceReadRepository permissionSources,
	RedisCache redisCache,
	IUnitOfWork unitOfWork,
	ILogger<CachedUserPermissionRepository> logger
) : IUserPermissionRepository
{
	private static readonly DistributedCacheEntryOptions Ttl = new DistributedCacheEntryOptions
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes: 3)
	};

	public Task<UserPermissionAggregate?> GetByUserIdAsync(
		Guid userId,
		CancellationToken ct = default
	) => inner.GetByUserIdAsync(userId: userId, ct: ct);

	public async Task SaveAsync(
		UserPermissionAggregate userPermission,
		CancellationToken ct = default)
	{
		await inner.SaveAsync(userPermission: userPermission, ct: ct);

		Guid userId = userPermission.UserId;
		IReadOnlySet<string> roleGrants = await permissionSources.GetRoleGrantsAsync(userId: userId, ct: ct);

		HashSet<string> effective = [..userPermission.Permissions, ..roleGrants];

		unitOfWork.OnCommitted(callback: async () =>
		{
			bool refreshed = await redisCache.SetAsync(
				key: PermissionCacheKeys.Permissions(userId: userId),
				value: effective,
				options: Ttl
			);

			if (!refreshed)
				logger.ZLogWarning(message: $"Permission cache for {userId} was not refreshed; the previous set stays in effect until its TTL expires.");
		});
	}
}
