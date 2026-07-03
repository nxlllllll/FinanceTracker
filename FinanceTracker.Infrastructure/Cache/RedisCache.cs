using System.Text.Json;
using FinanceTracker.Infrastructure.Configurations.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FinanceTracker.Infrastructure.Cache;

/// <summary>
/// Thin wrapper around <see cref="IConnectionMultiplexer"/> that handles JSON
/// serialization/deserialization and returns <see cref="CacheEntry{T}"/>
/// to distinguish cache hits from misses without null checks.
/// </summary>
public sealed class RedisCache(
	IConnectionMultiplexer connectionMultiplexer,
	IOptionsMonitor<RedisOptions> options)
{
	private string Prefixed(string key)
		=> $"{options.CurrentValue.InstanceName}{key}";

	private static TimeSpan ToExpiry(DistributedCacheEntryOptions cacheOptions)
	{
		if (cacheOptions.AbsoluteExpirationRelativeToNow is { } relative)
			return relative;

		if (cacheOptions.AbsoluteExpiration is { } absolute)
		{
			TimeSpan ttl = absolute - DateTimeOffset.UtcNow;
			return ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(value: 1);
		}

		throw new ArgumentException(
			message: $"""
				{nameof(RedisCache)} requires an explicit {nameof(DistributedCacheEntryOptions.AbsoluteExpiration)} or
				{nameof(DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow)}. Caching forever is not supported implicitly.
			""",
			paramName: nameof(cacheOptions)
		);
	}

	public async Task<CacheEntry<T>> TryGetAsync<T>(string key, CancellationToken ct = default)
	{
		IDatabase database = connectionMultiplexer.GetDatabase();
		RedisValue value = await database.StringGetAsync(key: Prefixed(key: key));

		if (value.IsNull)
			return new CacheEntry<T>(Found: false, Value: default!);

		return new CacheEntry<T>(
			Found: true,
			Value: JsonSerializer.Deserialize<T>(utf8Json: (byte[])value!)!
		);
	}

	public async Task SetAsync<T>(
		string key,
		T value,
		DistributedCacheEntryOptions options,
		CancellationToken ct = default)
	{
		IDatabase database = connectionMultiplexer.GetDatabase();
		await database.StringSetAsync(
			key: Prefixed(key: key),
			value: JsonSerializer.SerializeToUtf8Bytes(value: value),
			expiry: ToExpiry(cacheOptions: options)
		);
	}

	/// <summary>
	/// Reads multiple keys in a single <c>MGET</c> round-trip instead of one round-trip per key.
	/// The result always has exactly one entry per input key — missing keys come back as
	/// <see cref="CacheEntry{T}.Found"/> <c>false</c> rather than being omitted.
	/// </summary>
	public async Task<Dictionary<string, CacheEntry<T>>> TryGetBatchAsync<T>(
		IReadOnlyList<string> keys,
		CancellationToken ct = default)
	{
		if (keys.Count == 0)
			return [];

		RedisKey[] redisKeys = new RedisKey[keys.Count];
		for (int i = 0; i < keys.Count; i++)
			redisKeys[i] = Prefixed(key: keys[i]);

		IDatabase database = connectionMultiplexer.GetDatabase();
		RedisValue[] values = await database.StringGetAsync(keys: redisKeys);

		Dictionary<string, CacheEntry<T>> result = new Dictionary<string, CacheEntry<T>>(capacity: keys.Count);
		for (int i = 0; i < keys.Count; i++)
		{
			RedisValue value = values[i];
			result[keys[i]] = value.IsNull
				? new CacheEntry<T>(Found: false, Value: default!)
				: new CacheEntry<T>(Found: true, Value: JsonSerializer.Deserialize<T>(utf8Json: (byte[])value!)!);
		}

		return result;
	}

	/// <summary>
	/// Writes multiple keys in a single pipelined batch — one network flush, all commands
	/// awaited together, instead of one round-trip per key.
	/// </summary>
	public async Task SetBatchAsync<T>(
		IReadOnlyList<(string Key, T Value, DistributedCacheEntryOptions Options)> items,
		CancellationToken ct = default)
	{
		if (items.Count == 0)
			return;

		IDatabase database = connectionMultiplexer.GetDatabase();
		IBatch batch = database.CreateBatch();

		Task[] tasks = new Task[items.Count];
		for (int i = 0; i < items.Count; i++)
		{
			(string key, T value, DistributedCacheEntryOptions itemOptions) = items[i];
			tasks[i] = batch.StringSetAsync(
				key: Prefixed(key: key),
				value: JsonSerializer.SerializeToUtf8Bytes(value: value),
				expiry: ToExpiry(cacheOptions: itemOptions)
			);
		}

		batch.Execute();
		await Task.WhenAll(tasks);
	}
}
