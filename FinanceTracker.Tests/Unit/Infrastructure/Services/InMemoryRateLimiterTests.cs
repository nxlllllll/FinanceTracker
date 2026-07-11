using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Services.RateLimit;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class InMemoryRateLimiterTests
{
	private sealed class MutableDateProvider : IDateProvider
	{
		public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(year: 2024, month: 1, day: 15, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);
		public DateOnly UtcToday => DateOnly.FromDateTime(dateTime: UtcNow.UtcDateTime);
	}

	private MutableDateProvider _dateProvider = null!;
	private InMemoryRateLimiter _limiter = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_dateProvider = new MutableDateProvider();
		_limiter = new InMemoryRateLimiter(
			dateProvider: _dateProvider,
			options: new FakeOptionsMonitor<InMemoryRateLimiterOptions>(value: new InMemoryRateLimiterOptions())
		);
	}

	[Test]
	public async Task IsAllowedAsync_WithinLimit_ShouldAllow()
	{
		string key = $"test:{Guid.CreateVersion7():N}";

		bool result = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 3, windowSeconds: 60);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_ExceedingLimit_ShouldDeny()
	{
		string key = $"test:{Guid.CreateVersion7():N}";

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 2, windowSeconds: 60);
		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 2, windowSeconds: 60);
		bool thirdResult = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 2, windowSeconds: 60);

		await Assert.That(value: thirdResult).IsFalse();
	}

	[Test]
	public async Task IsAllowedAsync_AfterWindowExpires_ShouldAllowAgain()
	{
		string key = $"test:{Guid.CreateVersion7():N}";

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);
		bool deniedWithinWindow = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		_dateProvider.UtcNow = _dateProvider.UtcNow.AddSeconds(seconds: 61);
		bool allowedAfterExpiry = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: deniedWithinWindow).IsFalse();
		await Assert.That(value: allowedAfterExpiry).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_DifferentKeys_ShouldBeIndependent()
	{
		string keyA = $"test:a:{Guid.CreateVersion7():N}";
		string keyB = $"test:b:{Guid.CreateVersion7():N}";

		await _limiter.IsAllowedAsync(key: keyA, requestsPerWindow: 1, windowSeconds: 60);
		bool keyADenied = await _limiter.IsAllowedAsync(key: keyA, requestsPerWindow: 1, windowSeconds: 60);
		bool keyBAllowed = await _limiter.IsAllowedAsync(key: keyB, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: keyADenied).IsFalse();
		await Assert.That(value: keyBAllowed).IsTrue();
	}

	[Test]
	public async Task Clear_ShouldResetAllTrackedWindows()
	{
		string key = $"test:{Guid.CreateVersion7():N}";

		await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);
		bool deniedBeforeClear = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		_limiter.Clear();
		bool allowedAfterClear = await _limiter.IsAllowedAsync(key: key, requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: deniedBeforeClear).IsFalse();
		await Assert.That(value: allowedAfterClear).IsTrue();
	}

	[Test]
	public async Task IsAllowedAsync_UnderConcurrentLoad_ShouldAllowExactlyTheConfiguredLimit()
	{
		string key = $"test:{Guid.CreateVersion7():N}";
		const int limit = 20;
		const int concurrentCalls = 50;

		Task<bool>[] tasks = new Task<bool>[concurrentCalls];
		for (int i = 0; i < concurrentCalls; i++)
			tasks[i] = _limiter.IsAllowedAsync(key: key, requestsPerWindow: limit, windowSeconds: 60);

		bool[] results = await Task.WhenAll(tasks: tasks);

		await Assert.That(value: results.Count(predicate: allowed => allowed)).IsEqualTo(expected: limit);
	}

	[Test]
	public async Task IsAllowedAsync_WhenAtMaxTrackedKeys_ShouldDenyNeverSeenKey()
	{
		InMemoryRateLimiter limiter = new InMemoryRateLimiter(
			dateProvider: _dateProvider,
			options: new FakeOptionsMonitor<InMemoryRateLimiterOptions>(value: new InMemoryRateLimiterOptions
			{
				MaxTrackedKeys = 2,
				SweepIntervalCalls = 1_000_000
			})
		);

		await limiter.IsAllowedAsync(key: "key-a", requestsPerWindow: 10, windowSeconds: 60);
		await limiter.IsAllowedAsync(key: "key-b", requestsPerWindow: 10, windowSeconds: 60);
		bool thirdKeyAllowed = await limiter.IsAllowedAsync(key: "key-c", requestsPerWindow: 10, windowSeconds: 60);

		await Assert.That(value: thirdKeyAllowed).IsFalse().Because(message: """
			Once the table is at MaxTrackedKeys, a key we've never tracked before must be denied rather
			than growing the table further — this is the safety valve against unbounded key cardinality.
		""");
	}

	[Test]
	public async Task IsAllowedAsync_WhenAtMaxTrackedKeys_ShouldStillAllowAlreadyTrackedKey()
	{
		InMemoryRateLimiter limiter = new InMemoryRateLimiter(
			dateProvider: _dateProvider,
			options: new FakeOptionsMonitor<InMemoryRateLimiterOptions>(value: new InMemoryRateLimiterOptions
			{
				MaxTrackedKeys = 1,
				SweepIntervalCalls = 1_000_000
			})
		);

		await limiter.IsAllowedAsync(key: "key-a", requestsPerWindow: 10, windowSeconds: 60);
		bool sameKeyAllowedAgain = await limiter.IsAllowedAsync(key: "key-a", requestsPerWindow: 10, windowSeconds: 60);

		await Assert.That(value: sameKeyAllowedAgain).IsTrue().Because(message: """
			The cap only blocks growth from new keys — a key already in the table must keep working
			normally even while at capacity.
		""");
	}

	[Test]
	public async Task IsAllowedAsync_AfterKeyGoesIdlePastTimeout_ShouldReclaimItOnNextSweep()
	{
		InMemoryRateLimiter limiter = new InMemoryRateLimiter(
			dateProvider: _dateProvider,
			options: new FakeOptionsMonitor<InMemoryRateLimiterOptions>(value: new InMemoryRateLimiterOptions
			{
				MaxTrackedKeys = 1,
				SweepIntervalCalls = 2,
				KeyIdleTimeoutMs = 1_000
			})
		);

		await limiter.IsAllowedAsync(key: "key-a", requestsPerWindow: 1, windowSeconds: 60);
		_dateProvider.UtcNow = _dateProvider.UtcNow.AddMilliseconds(milliseconds: 2_000);
		bool newKeyAllowed = await limiter.IsAllowedAsync(key: "key-b", requestsPerWindow: 1, windowSeconds: 60);

		await Assert.That(value: newKeyAllowed).IsTrue().Because(message: """
			key-a went idle past KeyIdleTimeoutMs without ever being queried again, so the periodic
			sweep — not a lazy per-key trim — must be what reclaims its slot for a new key.
		""");
	}
}
