using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using Microsoft.Extensions.Caching.Distributed;
using UserPermissionAggregate = FinanceTracker.Core.Domains.UserPermission.UserPermission;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedUserPermissionRepository(
	IUserPermissionRepository inner,
	RedisCache redisCache,
	IUnitOfWork unitOfWork
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

		HashSet<string> permissions = [..userPermission.Permissions];
		Guid userId = userPermission.UserId;

		unitOfWork.OnCommitted(callback: () => redisCache.SetAsync(
			key: CachedUserPermissionReadRepository.KeyFor(userId: userId),
			value: permissions,
			options: Ttl
		));
	}
}
