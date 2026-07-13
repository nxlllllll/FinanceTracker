using FinanceTracker.Core.Services.RateLimit;
using StackExchange.Redis;

namespace FinanceTracker.Infrastructure.Services.RateLimit;

/// <summary>
/// Redis-backed sliding-window rate limiter using an atomic Lua script.
/// The script executes <c>TIME</c>, <c>ZREMRANGEBYSCORE</c>, <c>ZCARD</c>, <c>ZADD</c>, and
/// <c>PEXPIRE</c> in a single round-trip, eliminating race conditions between check and increment.
/// </summary>
public sealed class RedisRateLimiter(IConnectionMultiplexer connectionMultiplexer) : IRateLimiter
{
	private static readonly LuaScript SlidingWindowScript = LuaScript.Prepare(script: """
		local key = @key
		local windowMs = tonumber(@windowMs)
		local limit = tonumber(@limit)
		local unique = @unique
		local time = redis.call('TIME')
		local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
		redis.call('ZREMRANGEBYSCORE', key, '-inf', now - windowMs)
		local count = redis.call('ZCARD', key)
		if count < limit then
		    redis.call('ZADD', key, now, unique)
		    redis.call('PEXPIRE', key, windowMs + 1000)
		    return 1
		end
		return 0
		""");

	public async Task<bool> IsAllowedAsync(
		string key,
		int requestsPerWindow,
		int windowSeconds,
		CancellationToken ct = default)
	{
		IDatabase database = connectionMultiplexer.GetDatabase();

		long windowMs = windowSeconds * 1000L;

		RedisResult result = await database.ScriptEvaluateAsync(script: SlidingWindowScript, parameters: new
		{
			key = (RedisKey)key,
			windowMs,
			limit = requestsPerWindow,
#pragma warning disable RS0030 // Banned API — jitter only needs uniqueness, not time-ordering, so a random Guid is fine here.
			unique = Guid.NewGuid().ToString(format: "N")
#pragma warning restore RS0030
		});

		return (long)result == 1;
	}
}
