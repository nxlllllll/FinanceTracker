using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.AccountType;
using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

public sealed class CachedAccountTypeReadRepository(
	IAccountTypeReadRepository inner,
	RedisCache redisCache
) : IAccountTypeReadRepository
{
	private static readonly DistributedCacheEntryOptions Ttl = new()
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(value: 7)
	};

	private const string AllKey = "accounttypes:all";

	public async Task<IReadOnlyList<AccountTypeDto>> GetAllAsync(CancellationToken ct = default)
	{
		CacheEntry<IReadOnlyList<AccountTypeDto>> entry = await redisCache.TryGetAsync<IReadOnlyList<AccountTypeDto>>(key: AllKey, ct: ct);
		if (entry.Found) 
			return entry.Value ?? [];

		IReadOnlyList<AccountTypeDto> result = await inner.GetAllAsync(ct: ct);
		await redisCache.SetAsync(key: AllKey, value: result, options: Ttl, ct: ct);
		return result;
	}

	public async Task<AccountTypeDto?> GetByTypeAsync(string type, CancellationToken ct = default)
	{
		string key = $"accounttype:{type}";
		CacheEntry<AccountTypeDto?> entry = await redisCache.TryGetAsync<AccountTypeDto?>(key: key, ct: ct);
		if (entry.Found)
			return entry.Value;

		AccountTypeDto? result = await inner.GetByTypeAsync(type: type, ct: ct);
		await redisCache.SetAsync(key: key, value: result, options: Ttl, ct: ct);
		return result;
	}
}