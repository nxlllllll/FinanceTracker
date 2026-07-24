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
		).Returns(returnThis: RateLimitResult.Allowed());

		RateLimitResult result = await _limiter.IsAllowedAsync(
			key: "k",
			requestsPerWindow: 5,
			windowSeconds: 60
		);

		await Assert.That(value: result.IsAllowed).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerRespondsInTime_ShouldNotUseFallback()
	{
		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Denied(retryAfterSeconds: 42));

		RateLimitResult result = await _limiter.IsAllowedAsync(
			key: $"k:{Guid.CreateVersion7():N}",
			requestsPerWindow: 5,
			windowSeconds: 60
		);

		await Assert.That(value: result.IsAllowed).IsFalse();
		await Assert.That(value: result.RetryAfterSeconds).IsEqualTo(expected: 42);
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

		RateLimitResult result = await _limiter.IsAllowedAsync(
			key: $"k:{Guid.CreateVersion7():N}",
			requestsPerWindow: 5,
			windowSeconds: 60
		);

		await Assert.That(value: result.IsAllowed).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerThrowsAnUnrelatedException_ShouldStillFallBackToInMemory()
	{
		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(new InvalidOperationException(message: "Something unrelated to Redis broke."));

		RateLimitResult result = await _limiter.IsAllowedAsync(
			key: $"k:{Guid.CreateVersion7():N}",
			requestsPerWindow: 5,
			windowSeconds: 60
		);

		await Assert.That(value: result.IsAllowed).IsTrue().Because(message: """
			The fallback must degrade gracefully for ANY unexpected failure from the primary
			limiter, not just RedisException — a narrower catch would let an unrelated bug in the
			Redis path take down rate limiting entirely instead of degrading.
		""");
	}

	[Test]
	public async Task IsAllowedAsync_WhenCallerCancelsTheRequest_ShouldPropagateTheCancellation_NotFallBack()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();

		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			cts.Cancel();
			return Task.FromException<RateLimitResult>(exception: new OperationCanceledException(token: cts.Token));
		});

		await Assert.That(async () => await _limiter.IsAllowedAsync(
			key: $"k:{Guid.CreateVersion7():N}",
			requestsPerWindow: 5,
			windowSeconds: 60,
			ct: cts.Token
		)).Throws<OperationCanceledException>().Because(message: """
			A cancellation genuinely triggered by the caller's own token must propagate as-is —
			reinterpreting it as "Redis failed, fall back" would mask the real reason the
			request ended and could do unnecessary extra work on a request that's already gone.
		""");
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

		await _limiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: 1,
			windowSeconds: 60
		);
		RateLimitResult secondResult = await _limiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: 1,
			windowSeconds: 60
		);

		await Assert.That(value: secondResult.IsAllowed).IsFalse();
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
			return RateLimitResult.Allowed();
		});

		RateLimitResult result = await _limiter.IsAllowedAsync(
			key: $"k:{Guid.CreateVersion7():N}",
			requestsPerWindow: 5,
			windowSeconds: 60
		);

		await Assert.That(value: result.IsAllowed).IsTrue();
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

		await _limiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: 1,
			windowSeconds: 60
		);
		RateLimitResult deniedWhileDegraded = await _limiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: 1,
			windowSeconds: 60
		);

		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Allowed());

		await _limiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: 1,
			windowSeconds: 60
		);

		_inner.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(new RedisConnectionException(failureType: ConnectionFailureType.SocketFailure, message: "Connection lost again."));

		RateLimitResult allowedAfterClear = await _limiter.IsAllowedAsync(
			key: key,
			requestsPerWindow: 1,
			windowSeconds: 60
		);

		await Assert.That(value: deniedWhileDegraded.IsAllowed).IsFalse();
		await Assert.That(value: allowedAfterClear.IsAllowed).IsTrue();
	}
}
