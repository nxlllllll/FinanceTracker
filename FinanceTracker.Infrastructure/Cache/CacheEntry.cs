namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Result of a cache lookup. Avoids returning <c>null</c> for cache misses on value types.
/// </summary>
/// <param name="Found"><c>true</c> if the key existed in the cache.</param>
/// <param name="Value">The cached value. Undefined when <paramref name="Found"/> is <c>false</c>.</param>
public readonly record struct CacheEntry<T>(bool Found, T Value);
