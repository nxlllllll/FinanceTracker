using FinanceTracker.Infrastructure.Services.RateLimit;
using FinanceTracker.Tests.Integration._Shared.Fixtures;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

[NotInParallel]
public sealed class RedisRateLimiterTests : RedisFixture
{
	private RedisRateLimiter _rateLimiter = null!;

	[Before(hookType: Test)]
	public void Setup()
		=> _rateLimiter = new RedisRateLimiter(connectionMultiplexer: Redis);

	[Test]
	public async Task IsAllowedAsync_WhenUnderLimit_ShouldAllow()
	{
		string key = $"test:ratelimit:{Guid.CreateVersion7():N}";

		bool result = await _rateLimiter.IsAllowedAsync(key: key, requestsPerWindow: 5, windowSeconds: 10);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_WhenLimitReached_ShouldDeny()
	{
		string key = $"test:ratelimit:{Guid.CreateVersion7():N}";
		const int limit = 3;

		for (int i = 0; i < limit; i++)
			await _rateLimiter.IsAllowedAsync(key: key, requestsPerWindow: limit, windowSeconds: 10);

		bool result = await _rateLimiter.IsAllowedAsync(key: key, requestsPerWindow: limit, windowSeconds: 10);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task IsAllowedAsync_DifferentKeys_ShouldTrackIndependently()
	{
		string key1 = $"test:ratelimit:{Guid.CreateVersion7():N}";
		string key2 = $"test:ratelimit:{Guid.CreateVersion7():N}";
		const int limit = 1;

		await _rateLimiter.IsAllowedAsync(key: key1, requestsPerWindow: limit, windowSeconds: 10);
		bool key1Allowed = await _rateLimiter.IsAllowedAsync(key: key1, requestsPerWindow: limit, windowSeconds: 10);
		bool key2Allowed = await _rateLimiter.IsAllowedAsync(key: key2, requestsPerWindow: limit, windowSeconds: 10);

		await Assert.That(value: key1Allowed).IsFalse();
		await Assert.That(value: key2Allowed).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_AfterWindowElapses_ShouldAllowAgain()
	{
		string key = $"test:ratelimit:{Guid.CreateVersion7():N}";
		const int limit = 1;
		const int windowSeconds = 1;

		bool firstAllowed = await _rateLimiter.IsAllowedAsync(key: key, requestsPerWindow: limit, windowSeconds: windowSeconds);
		bool immediatelyDenied = await _rateLimiter.IsAllowedAsync(key: key, requestsPerWindow: limit, windowSeconds: windowSeconds);

		await Task.Delay(delay: TimeSpan.FromSeconds(value: windowSeconds + 1));

		bool allowedAfterWindow = await _rateLimiter.IsAllowedAsync(key: key, requestsPerWindow: limit, windowSeconds: windowSeconds);

		await Assert.That(value: firstAllowed).IsTrue();
		await Assert.That(value: immediatelyDenied).IsFalse();
		await Assert.That(value: allowedAfterWindow).IsTrue();
	}
}
