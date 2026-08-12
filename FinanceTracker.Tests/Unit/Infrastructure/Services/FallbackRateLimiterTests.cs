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
		ProbeTimeoutMs = 100,
		RecoveryProbeIntervalMs = 0
	};

	private const long DegradedCallBudgetMs = 250;

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

	private void InnerReturns(RateLimitResult result) => _inner.IsAllowedAsync(
		key: Arg.Any<string>(),
		requestsPerWindow: Arg.Any<int>(),
		windowSeconds: Arg.Any<int>(),
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: result);

	private void InnerThrows(Exception exception) => _inner.IsAllowedAsync(
		key: Arg.Any<string>(),
		requestsPerWindow: Arg.Any<int>(),
		windowSeconds: Arg.Any<int>(),
		ct: Arg.Any<CancellationToken>()
	).ThrowsAsync(ex: exception);

	private void InnerHangs(int delayMs = 500) => _inner.IsAllowedAsync(
		key: Arg.Any<string>(),
		requestsPerWindow: Arg.Any<int>(),
		windowSeconds: Arg.Any<int>(),
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: async _ =>
	{
		await Task.Delay(delay: TimeSpan.FromMilliseconds(value: delayMs));
		return RateLimitResult.Allowed();
	});

	private static RedisConnectionException RedisDown(string message = "Connection lost.") => new RedisConnectionException(
		failureType: ConnectionFailureType.SocketFailure,
		message: message,
		flags: CommandFlags.None
	);

	private async Task<bool> WaitUntilRecoveredAsync()
	{
		for (int attempt = 0; attempt < 100; attempt++)
		{
			if (!_limiter.IsDegraded)
				return true;

			await Task.Delay(delay: TimeSpan.FromMilliseconds(value: 20));
		}

		return false;
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerRespondsInTime_ShouldReturnInnerResult()
	{
		InnerReturns(result: RateLimitResult.Allowed());

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
		InnerReturns(result: RateLimitResult.Denied(retryAfterSeconds: 42));

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
		InnerThrows(exception: RedisDown());

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
		InnerThrows(exception: new InvalidOperationException(message: "Something unrelated to Redis broke."));

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
		InnerThrows(exception: RedisDown());

		string key = $"k:{Guid.CreateVersion7():N}";

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);
		RateLimitResult secondResult = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: secondResult.IsAllowed).IsFalse();
	}

	[Test]
	public async Task IsAllowedAsync_WhenInnerExceedsProbeTimeout_ShouldFallBackToInMemory()
	{
		InnerHangs();

		RateLimitResult result = await _limiter.IsAllowedAsync(
			key: $"k:{Guid.CreateVersion7():N}",
			requestsPerWindow: 5,
			windowSeconds: 60
		);

		await Assert.That(value: result.IsAllowed).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_WhenRedisFails_ShouldReportDegraded()
	{
		InnerThrows(exception: RedisDown());

		await _limiter.IsAllowedAsync(
			key: $"k:{Guid.CreateVersion7():N}",
			requestsPerWindow: 5,
			windowSeconds: 60
		);

		await Assert.That(value: _limiter.IsDegraded).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_WhileDegraded_ShouldNotPayTheProbeTimeoutPerRequest()
	{
		InnerHangs();

		await _limiter.IsAllowedAsync(key: "warmup", requestsPerWindow: 100, windowSeconds: 60);

		long start = Environment.TickCount64;

		for (int i = 0; i < 5; i++)
			await _limiter.IsAllowedAsync(key: $"k:{Guid.CreateVersion7():N}", requestsPerWindow: 100, windowSeconds: 60);

		long elapsed = Environment.TickCount64 - start;

		await Assert.That(value: elapsed).IsLessThan(maximum: DegradedCallBudgetMs).Because(message: """
			Without the breaker these five calls would each wait out ProbeTimeoutMs before falling
			through — half a second of added latency spread across every request for as long as Redis
			stays down, and this limiter sits near the front of the pipeline.
		""");
	}

	[Test]
	public async Task IsAllowedAsync_AfterRecoveringFromFailure_ShouldClearFallbackState()
	{
		string exhausted = $"a:{Guid.CreateVersion7():N}";
		string recovering = $"b:{Guid.CreateVersion7():N}";

		InnerThrows(exception: RedisDown());

		await _limiter.IsAllowedAsync(key: exhausted, requestsPerWindow: 1, windowSeconds: 60);
		RateLimitResult deniedWhileDegraded = await _limiter.IsAllowedAsync(key: exhausted, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: deniedWhileDegraded.IsAllowed).IsFalse();

		InnerReturns(result: RateLimitResult.Allowed());

		await _limiter.IsAllowedAsync(key: recovering, requestsPerWindow: 1, windowSeconds: 60);

		bool recovered = await WaitUntilRecoveredAsync();
		await Assert.That(value: recovered).IsTrue();

		InnerThrows(exception: RedisDown(message: "Connection lost again."));

		RateLimitResult afterClear = await _limiter.IsAllowedAsync(key: exhausted, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: afterClear.IsAllowed).IsTrue().Because(message: """
			The exhausted window has to be gone once Redis is authoritative again. Remove the Clear()
			call and this key stays blocked for the rest of its window for no reason.
		""");
	}

	[Test]
	public async Task IsAllowedAsync_AfterRecoveringFromFailure_ShouldGoBackToRedis()
	{
		string key = $"k:{Guid.CreateVersion7():N}";

		InnerThrows(exception: RedisDown());

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);
		await Assert.That(value: _limiter.IsDegraded).IsTrue();

		InnerReturns(result: RateLimitResult.Allowed());

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		bool recovered = await WaitUntilRecoveredAsync();
		await Assert.That(value: recovered).IsTrue();

		InnerReturns(result: RateLimitResult.Denied(retryAfterSeconds: 7));

		RateLimitResult afterRecovery = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: afterRecovery.RetryAfterSeconds).IsEqualTo(expected: 7);
	}
}
