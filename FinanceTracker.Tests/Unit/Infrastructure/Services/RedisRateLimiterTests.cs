using FinanceTracker.Infrastructure.Services.Date;
using FinanceTracker.Infrastructure.Services.RateLimit;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class RedisRateLimiterTests : RedisFixture
{
	private RedisRateLimiter _rateLimiter = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_rateLimiter = new RedisRateLimiter(
			connectionMultiplexer: Redis,
			dateProvider: new DateProvider()
		);
	}

	[Test]
	public async Task IsAllowedAsync_WhenUnderLimit_ShouldAllow()
	{
		string key = $"test:ratelimit:{Guid.CreateVersion7():N}";

		bool result = await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: 5,
			windowSeconds: 60
		);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_WhenLimitReached_ShouldDeny()
	{
		string key = $"test:ratelimit:{Guid.CreateVersion7():N}";
		const int limit = 3;

		for (int i = 0; i < limit; i++)
		{
			await _rateLimiter.IsAllowedAsync(
				key: key,
				requestsPerWindow: limit,
				windowSeconds: 60
			);
		}

		bool result = await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: limit,
			windowSeconds: 60
		);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task IsAllowedAsync_DifferentKeys_ShouldTrackIndependently()
	{
		string key1 = $"test:ratelimit:{Guid.CreateVersion7():N}";
		string key2 = $"test:ratelimit:{Guid.CreateVersion7():N}";
		const int limit = 1;

		await _rateLimiter.IsAllowedAsync(key: key1, requestsPerWindow: limit, windowSeconds: 60);
		bool key1Denied = await _rateLimiter.IsAllowedAsync(key: key1, requestsPerWindow: limit, windowSeconds: 60);
		bool key2Allowed = await _rateLimiter.IsAllowedAsync(key: key2, requestsPerWindow: limit, windowSeconds: 60);

		await Assert.That(value: key1Denied).IsFalse();
		await Assert.That(value: key2Allowed).IsTrue();
	}
}