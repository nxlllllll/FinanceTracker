using FinanceTracker.Core.Services.RateLimit;
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

		RateLimitResult result = await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: 5,
			windowSeconds: 10
		);

		await Assert.That(value: result.IsAllowed).IsTrue();
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
				windowSeconds: 10
			);
		}

		RateLimitResult result = await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: limit,
			windowSeconds: 10
		);

		await Assert.That(value: result.IsAllowed).IsFalse();
	}

	[Test]
	public async Task IsAllowedAsync_WhenLimitReached_ShouldReturnRetryAfterWithinTheConfiguredWindow()
	{
		string key = $"test:ratelimit:{Guid.CreateVersion7():N}";
		const int limit = 1;
		const int windowSeconds = 10;

		await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: limit,
			windowSeconds: windowSeconds
		);
		RateLimitResult result = await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: limit,
			windowSeconds: windowSeconds
		);

		await Assert.That(value: result.IsAllowed).IsFalse();
		await Assert.That(value: result.RetryAfterSeconds).IsGreaterThan(minimum: 0);
		await Assert.That(value: result.RetryAfterSeconds).IsLessThanOrEqualTo(maximum: windowSeconds);
	}

	[Test]
	public async Task IsAllowedAsync_DifferentKeys_ShouldTrackIndependently()
	{
		string key1 = $"test:ratelimit:{Guid.CreateVersion7():N}";
		string key2 = $"test:ratelimit:{Guid.CreateVersion7():N}";
		const int limit = 1;

		await _rateLimiter.IsAllowedAsync(
			key: key1,
			requestsPerWindow: limit,
			windowSeconds: 10
		);
		RateLimitResult key1Result = await _rateLimiter.IsAllowedAsync(
			key: key1,
			requestsPerWindow: limit,
			windowSeconds: 10
		);
		RateLimitResult key2Result = await _rateLimiter.IsAllowedAsync(
			key: key2,
			requestsPerWindow: limit,
			windowSeconds: 10
		);

		await Assert.That(value: key1Result.IsAllowed).IsFalse();
		await Assert.That(value: key2Result.IsAllowed).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_AfterWindowElapses_ShouldAllowAgain()
	{
		string key = $"test:ratelimit:{Guid.CreateVersion7():N}";
		const int limit = 1;
		const int windowSeconds = 1;

		RateLimitResult first = await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: limit,
			windowSeconds: windowSeconds
		);
		RateLimitResult immediatelyDenied = await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: limit,
			windowSeconds: windowSeconds
		);

		await Task.Delay(delay: TimeSpan.FromSeconds(value: windowSeconds + 1));

		RateLimitResult allowedAfterWindow = await _rateLimiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: limit,
			windowSeconds: windowSeconds
		);

		await Assert.That(value: first.IsAllowed).IsTrue();
		await Assert.That(value: immediatelyDenied.IsAllowed).IsFalse();
		await Assert.That(value: allowedAfterWindow.IsAllowed).IsTrue();
	}
}
