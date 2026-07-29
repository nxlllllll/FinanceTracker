using Microsoft.Extensions.Caching.Distributed;

namespace FinanceTracker.Infrastructure.Cache;

public sealed record BatchItem<T>(
	string Key,
	T Value,
	DistributedCacheEntryOptions Options
);
