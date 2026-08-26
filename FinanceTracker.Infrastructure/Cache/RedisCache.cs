using System.Text.Json;
using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Configurations.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
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
	IOptionsMonitor<RedisOptions> options,
	IDateProvider dateProvider,
	ILogger<RedisCache> logger)
{
	private string Prefixed(string key)
		=> $"{options.CurrentValue.InstanceName}{key}";

	private TimeSpan ToExpiry(DistributedCacheEntryOptions cacheOptions)
	{
		if (cacheOptions.AbsoluteExpirationRelativeToNow is { } relative)
			return relative;

		if (cacheOptions.AbsoluteExpiration is { } absolute)
		{
			TimeSpan ttl = absolute - dateProvider.UtcNow;
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

	private static void RecordFailure(string operation) => FinanceTrackerMetrics.CacheOperationFailures.Add(
		delta: 1,
		tag: new KeyValuePair<string, object?>(key: FinanceTrackerMetrics.Tags.Operation, value: operation)
	);

	private CacheEntry<T> ToEntry<T>(string key, RedisValue value)
	{
		try
		{
			return new CacheEntry<T>(Found: true, Value: JsonSerializer.Deserialize<T>(utf8Json: (byte[])value!)!);
		}
		catch (JsonException ex)
		{
			RecordFailure(operation: FinanceTrackerMetrics.CacheOperations.Read);
			logger.LogWarning(exception: ex, message: "Unreadable value under key {Key} — reporting a cache miss and dropping it.", Prefixed(key: key));

			_ = DeleteBatchAsync(keys: [key]);

			return new CacheEntry<T>(Found: false, Value: default!);
		}
	}

	public async Task<CacheEntry<T>> TryGetAsync<T>(string key)
	{
		IDatabase database = connectionMultiplexer.GetDatabase();

		RedisValue value;
		try
		{
			value = await database.StringGetAsync(key: Prefixed(key: key));
		}
		catch (RedisException ex)
		{
			RecordFailure(operation: FinanceTrackerMetrics.CacheOperations.Read);
			logger.LogWarning(exception: ex, message: "Redis unavailable reading key {Key} — reporting a cache miss.", Prefixed(key: key));
			return new CacheEntry<T>(Found: false, Value: default!);
		}

		if (value.IsNull)
			return new CacheEntry<T>(Found: false, Value: default!);

		return ToEntry<T>(key: key, value: value);
	}

	public async Task<bool> SetAsync<T>(
		string key,
		T value,
		DistributedCacheEntryOptions options)
	{
		IDatabase database = connectionMultiplexer.GetDatabase();
		try
		{
			await database.StringSetAsync(
				key: Prefixed(key: key),
				value: JsonSerializer.SerializeToUtf8Bytes(value: value),
				expiry: ToExpiry(cacheOptions: options)
			);
			return true;
		}
		catch (RedisException ex)
		{
			RecordFailure(operation: FinanceTrackerMetrics.CacheOperations.Write);
			logger.LogWarning(exception: ex, message: "Redis unavailable writing key {Key} — the previous value stays until its TTL expires.", Prefixed(key: key));
			return false;
		}
	}

	/// <summary>
	/// Reads multiple keys in a single <c>MGET</c> round-trip instead of one round-trip per key.
	/// The result always has exactly one entry per input key — missing keys come back as
	/// <see cref="CacheEntry{T}.Found"/> <c>false</c> rather than being omitted.
	/// </summary>
	public async Task<Dictionary<string, CacheEntry<T>>> TryGetBatchAsync<T>(IReadOnlyList<string> keys)
	{
		if (keys.Count == 0)
			return [];

		RedisKey[] redisKeys = new RedisKey[keys.Count];
		for (int i = 0; i < keys.Count; i++)
			redisKeys[i] = Prefixed(key: keys[i]);

		IDatabase database = connectionMultiplexer.GetDatabase();

		RedisValue[] values;
		try
		{
			values = await database.StringGetAsync(keys: redisKeys);
		}
		catch (RedisException ex)
		{
			RecordFailure(operation: FinanceTrackerMetrics.CacheOperations.Read);
			logger.LogWarning(exception: ex, message: "Redis unavailable reading a batch of {Count} keys — reporting all as cache misses.", keys.Count);
			Dictionary<string, CacheEntry<T>> allMissed = new Dictionary<string, CacheEntry<T>>(capacity: keys.Count);
			foreach (string k in keys)
				allMissed[k] = new CacheEntry<T>(Found: false, Value: default!);
			return allMissed;
		}

		Dictionary<string, CacheEntry<T>> result = new Dictionary<string, CacheEntry<T>>(capacity: keys.Count);
		for (int i = 0; i < keys.Count; i++)
		{
			RedisValue value = values[i];
			result[keys[i]] = value.IsNull
				? new CacheEntry<T>(Found: false, Value: default!)
				: ToEntry<T>(key: keys[i], value: value);
		}

		return result;
	}

	/// <summary>
	/// Writes multiple keys in a single pipelined batch — one network flush, all commands
	/// awaited together, instead of one round-trip per key.
	/// </summary>
	public async Task<bool> SetBatchAsync<T>(IReadOnlyList<BatchItem<T>> items)
	{
		if (items.Count == 0)
			return true;

		IDatabase database = connectionMultiplexer.GetDatabase();
		IBatch batch = database.CreateBatch();

		Task[] tasks = new Task[items.Count];
		for (int i = 0; i < items.Count; i++)
		{
			BatchItem<T> item = items[i];
			tasks[i] = batch.StringSetAsync(
				key: Prefixed(key: item.Key),
				value: JsonSerializer.SerializeToUtf8Bytes(value: item.Value),
				expiry: ToExpiry(cacheOptions: item.Options)
			);
		}

		batch.Execute();

		try
		{
			await Task.WhenAll(tasks);
			return true;
		}
		catch (RedisException ex)
		{
			RecordFailure(operation: FinanceTrackerMetrics.CacheOperations.Write);
			logger.LogWarning(exception: ex, message: "Redis unavailable writing a batch of {Count} keys — previous values stay until their TTL expires.", items.Count);
			return false;
		}
	}

	public async Task<bool> DeleteBatchAsync(IReadOnlyList<string> keys)
	{
		if (keys.Count == 0)
			return true;

		RedisKey[] redisKeys = new RedisKey[keys.Count];
		for (int i = 0; i < keys.Count; i++)
			redisKeys[i] = Prefixed(key: keys[i]);

		IDatabase database = connectionMultiplexer.GetDatabase();
		try
		{
			await database.KeyDeleteAsync(keys: redisKeys);
			return true;
		}
		catch (RedisException ex)
		{
			RecordFailure(operation: FinanceTrackerMetrics.CacheOperations.Delete);
			logger.LogWarning(exception: ex, message: "Redis unavailable deleting a batch of {Count} keys — stale entries may remain until their TTL expires.", keys.Count);
			return false;
		}
	}
}
