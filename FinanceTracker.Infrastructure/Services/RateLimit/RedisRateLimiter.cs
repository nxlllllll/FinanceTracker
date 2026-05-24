using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.RateLimit;
using StackExchange.Redis;

namespace FinanceTracker.Infrastructure.Services.RateLimit;

public sealed class RedisRateLimiter(
	IConnectionMultiplexer connectionMultiplexer,
	IDateProvider dateProvider
) : IRateLimiter
{
	private static readonly LuaScript SlidingWindowScript = LuaScript.Prepare(script: """
		local key = @key
		local now = tonumber(@now)
		local windowMs = tonumber(@windowMs)
		local limit = tonumber(@limit)
		local unique = @unique
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

		long now = ((DateTimeOffset)dateProvider.UtcNow).ToUnixTimeMilliseconds();
		long windowMs = windowSeconds * 1000L;

		RedisResult result = await database.ScriptEvaluateAsync(script: SlidingWindowScript, parameters: new
		{
			key = (RedisKey)key,
			now,
			windowMs,
			limit = requestsPerWindow,
			unique = Guid.NewGuid().ToString(format: "N")
		});

		return (long)result == 1;
	}
}