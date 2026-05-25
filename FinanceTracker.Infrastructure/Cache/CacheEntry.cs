namespace FinanceTracker.Infrastructure.Cache;

public readonly record struct CacheEntry<T>(bool Found, T Value);
