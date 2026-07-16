using FinanceTracker.Core.Services.RateLimit;
using FinanceTracker.Infrastructure.Services.Date;
using FinanceTracker.Infrastructure.Services.RateLimit;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class FallbackRateLimiterTests
{
	private static readonly RateLimiterFallbackOptions DefaultOptions = new RateLimiterFallbackOptions
	{
		ProbeTimeoutMs = 100
	};

	private IRateLimiter _inner = null!;
	private InMemoryRateLimiter _fallback = null!;
	private FallbackRateLimiter _limiter = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<IRateLimiter>();
		_fallback = new InMemoryRateLimiter(
			dateProvider: new DateProvider(),
			options: new FakeOptionsMonitor<InMemoryRateLimiterOptions>(value: new InMemoryRateLimiterOptions())
		);

		_limiter = new FallbackRateLimiter(
			inner: _inner,
			fallback: _fallback,
			options: new FakeOptionsMonitor<RateLimiterFallbackOptions>(value: DefaultOptions),
			logger: NullLogger<FallbackRateLimiter>.Instance
		);
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerRespondsInTime_ShouldReturnInnerResult()
	{
		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		bool result = await _limiter.IsAllowedAsync(key: "k", requestsPerWindow: 5, windowSeconds: 60);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerRespondsInTime_ShouldNotUseFallback()
	{
		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		bool result = await _limiter.IsAllowedAsync(key: $"k:{Guid.CreateVersion7():N}", requestsPerWindow: 5, windowSeconds: 60);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerThrowsRedisException_ShouldFallBackToInMemory()
	{
		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(new RedisConnectionException(failureType: ConnectionFailureType.SocketFailure, message: "Connection lost."));

		bool result = await _limiter.IsAllowedAsync(key: $"k:{Guid.CreateVersion7():N}", requestsPerWindow: 5, windowSeconds: 60);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerThrowsRedisException_ShouldEnforceLimitViaFallback()
	{
		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(new RedisConnectionException(failureType: ConnectionFailureType.SocketFailure, message: "Connection lost."));

		string key = $"k:{Guid.CreateVersion7():N}";

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);
		bool secondResult = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: secondResult).IsFalse();
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerExceedsProbeTimeout_ShouldFallBackToInMemory()
	{
		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: async _ =>
		{
			await Task.Delay(delay: TimeSpan.FromMilliseconds(value: 500));
			return true;
		});

		bool result = await _limiter.IsAllowedAsync(key: $"k:{Guid.CreateVersion7():N}", requestsPerWindow: 5, windowSeconds: 60);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_AfterRecoveringFromFailure_ShouldClearFallbackState()
	{
		string key = $"k:{Guid.CreateVersion7():N}";

		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(new RedisConnectionException(failureType: ConnectionFailureType.SocketFailure, message: "Connection lost."));

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);
		bool deniedWhileDegraded = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(new RedisConnectionException(failureType: ConnectionFailureType.SocketFailure, message: "Connection lost again."));

		bool allowedAfterClear = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: deniedWhileDegraded).IsFalse();
		await Assert.That(value: allowedAfterClear).IsTrue();
	}
}
