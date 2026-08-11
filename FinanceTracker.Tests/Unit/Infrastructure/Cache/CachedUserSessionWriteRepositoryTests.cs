using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedUserSessionWriteRepositoryTests
{
	private IUserSessionWriteRepository _inner = null!;
	private IUnitOfWork _unitOfWork = null!;
	private List<Func<Task>?> _committedCallbacks = null!;
	private IDatabase _database = null!;
	private CachedUserSessionWriteRepository _repository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<IUserSessionWriteRepository>();
		_committedCallbacks = [];
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.When(
			substituteCall: uow => uow.OnCommitted(callback: Arg.Any<Func<Task>>())
		).Do(
			callbackWithArguments: call => _committedCallbacks.Add(item: call.Arg<Func<Task>>())
		);

		_database = Substitute.For<IDatabase>();

		IConnectionMultiplexer connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		connectionMultiplexer.GetDatabase(
			db: Arg.Any<int>(),
			asyncState: Arg.Any<object>()
		).Returns(returnThis: _database);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions { InstanceName = "ft_test:" });

		RedisCache redisCache = new RedisCache(
			connectionMultiplexer: connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);

		_repository = new CachedUserSessionWriteRepository(
			inner: _inner,
			redisCache: redisCache,
			unitOfWork: _unitOfWork
		);
	}

	private List<RedisKey> CaptureDeletedKeys()
	{
		List<RedisKey> captured = [];
		_database.KeyDeleteAsync(
			keys: Arg.Do<RedisKey[]>(useArgument: keys => captured.AddRange(collection: keys)),
			flags: Arg.Any<CommandFlags>()
		).Returns(returnThis: 0L);
		return captured;
	}

	private async Task SimulateCommitAsync()
	{
		foreach (Func<Task> callback in _committedCallbacks.OfType<Func<Task>>())
			await callback();
	}

	private async Task AssertRedisUntouchedAsync() => await _database.DidNotReceive().KeyDeleteAsync(
		keys: Arg.Any<RedisKey[]>(),
		flags: Arg.Any<CommandFlags>()
	);

	[Test]
	public async Task CreateAsync_ShouldDelegateToInner_WithoutTouchingRedis()
	{
		UserSession session = UserSession.Create(
			userId: Guid.CreateVersion7(),
			refreshTokenHash: "hash",
			expiresAt: DateTimeOffset.UtcNow.AddDays(days: 7),
			createdAt: FakeDateProvider.Default.UtcNow
		);

		await _repository.CreateAsync(session: session);

		await _inner.Received(requiredNumberOfCalls: 1).CreateAsync(
			session: session,
			ct: Arg.Any<CancellationToken>()
		);
		await Assert.That(value: _committedCallbacks).IsEmpty();
		await AssertRedisUntouchedAsync();
	}

	[Test]
	public async Task RevokeAsync_BeforeTransactionCommits_ShouldNotTouchRedis()
	{
		Guid sessionId = Guid.CreateVersion7();
		_inner.RevokeAsync(
			sessionId: sessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IReadOnlyList<Guid>)[sessionId]);

		await _repository.RevokeAsync(sessionId: sessionId, revokedAt: DateTimeOffset.UtcNow);

		await Assert.That(value: _committedCallbacks).Count().IsEqualTo(expected: 1);
		await AssertRedisUntouchedAsync();
	}

	[Test]
	public async Task RevokeAsync_WhenSessionWasRevoked_ShouldEvictItsActiveMarkOnCommit()
	{
		Guid sessionId = Guid.CreateVersion7();
		_inner.RevokeAsync(
			sessionId: sessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [sessionId]);

		List<RedisKey> deletedKeys = CaptureDeletedKeys();

		await _repository.RevokeAsync(sessionId: sessionId, revokedAt: DateTimeOffset.UtcNow);
		await SimulateCommitAsync();

		await Assert.That(value: deletedKeys).Count().IsEqualTo(expected: 1);
		await Assert.That(value: (string)deletedKeys[0]!).IsEqualTo(expected: $"ft_test:active-session:{sessionId}");
	}

	[Test]
	public async Task RevokeAsync_WhenNothingWasRevoked_ShouldNotRegisterACallback()
	{
		Guid sessionId = Guid.CreateVersion7();
		_inner.RevokeAsync(
			sessionId: sessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		await _repository.RevokeAsync(sessionId: sessionId, revokedAt: DateTimeOffset.UtcNow);
		await SimulateCommitAsync();

		await Assert.That(value: _committedCallbacks).IsEmpty();
		await AssertRedisUntouchedAsync();
	}

	[Test]
	public async Task RevokeAsync_ShouldReturnWhatInnerReturned()
	{
		Guid sessionId = Guid.CreateVersion7();
		IReadOnlyList<Guid> expected = [sessionId];
		_inner.RevokeAsync(
			sessionId: sessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: expected);

		IReadOnlyList<Guid> result = await _repository.RevokeAsync(sessionId: sessionId, revokedAt: DateTimeOffset.UtcNow);

		await Assert.That(value: result).IsEqualTo(expected: expected);
	}

	[Test]
	public async Task SupersedeAsync_WhenSessionWasRotated_ShouldEvictItsActiveMarkOnCommit()
	{
		Guid sessionId = Guid.CreateVersion7();
		Guid successorSessionId = Guid.CreateVersion7();
		_inner.SupersedeAsync(
			sessionId: sessionId,
			successorSessionId: successorSessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [sessionId]);

		List<RedisKey> deletedKeys = CaptureDeletedKeys();

		await _repository.SupersedeAsync(
			sessionId: sessionId,
			successorSessionId: successorSessionId,
			revokedAt: DateTimeOffset.UtcNow
		);
		await SimulateCommitAsync();

		await Assert.That(value: deletedKeys.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:active-session:{sessionId}");
	}

	[Test]
	public async Task RevokeAllExceptAsync_ShouldEvictEveryRevokedSessionOnCommit()
	{
		Guid userId = Guid.CreateVersion7();
		Guid exceptSessionId = Guid.CreateVersion7();
		Guid revoked1 = Guid.CreateVersion7();
		Guid revoked2 = Guid.CreateVersion7();

		_inner.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: exceptSessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [revoked1, revoked2]);

		List<RedisKey> deletedKeys = CaptureDeletedKeys();

		await _repository.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: exceptSessionId,
			revokedAt: DateTimeOffset.UtcNow
		);
		await SimulateCommitAsync();

		await Assert.That(value: deletedKeys).Count().IsEqualTo(expected: 2);
		await Assert.That(value: deletedKeys.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:active-session:{revoked1}");
		await Assert.That(value: deletedKeys.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:active-session:{revoked2}");
	}

	[Test]
	public async Task RevokeAllExceptAsync_BeforeTransactionCommits_ShouldNotTouchRedis()
	{
		Guid userId = Guid.CreateVersion7();
		Guid exceptSessionId = Guid.CreateVersion7();

		_inner.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: exceptSessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [Guid.CreateVersion7()]);

		await _repository.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: exceptSessionId,
			revokedAt: DateTimeOffset.UtcNow
		);

		await Assert.That(value: _committedCallbacks).Count().IsEqualTo(expected: 1);
		await AssertRedisUntouchedAsync();
	}

	[Test]
	public async Task RevokeAllAsync_ShouldEvictEveryRevokedSessionOnCommit()
	{
		Guid userId = Guid.CreateVersion7();
		Guid revoked1 = Guid.CreateVersion7();
		Guid revoked2 = Guid.CreateVersion7();

		_inner.RevokeAllAsync(
			userId: userId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [revoked1, revoked2]);

		List<RedisKey> deletedKeys = CaptureDeletedKeys();

		await _repository.RevokeAllAsync(userId: userId, revokedAt: DateTimeOffset.UtcNow);
		await SimulateCommitAsync();

		await Assert.That(value: deletedKeys).Count().IsEqualTo(expected: 2);
	}

	[Test]
	public async Task RevokeAllAsync_BeforeTransactionCommits_ShouldNotTouchRedis()
	{
		Guid userId = Guid.CreateVersion7();
		_inner.RevokeAllAsync(
			userId: userId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [Guid.CreateVersion7(), Guid.CreateVersion7()]);

		await _repository.RevokeAllAsync(userId: userId, revokedAt: DateTimeOffset.UtcNow);

		await Assert.That(value: _committedCallbacks).Count().IsEqualTo(expected: 1);
		await AssertRedisUntouchedAsync();
	}

	[Test]
	public async Task RevokeAllAsync_WhenNothingWasRevoked_ShouldNotRegisterACallback()
	{
		Guid userId = Guid.CreateVersion7();
		_inner.RevokeAllAsync(
			userId: userId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IReadOnlyList<Guid>)[]);

		await _repository.RevokeAllAsync(userId: userId, revokedAt: DateTimeOffset.UtcNow);
		await SimulateCommitAsync();

		await Assert.That(value: _committedCallbacks).IsEmpty();
		await AssertRedisUntouchedAsync();
	}
}
