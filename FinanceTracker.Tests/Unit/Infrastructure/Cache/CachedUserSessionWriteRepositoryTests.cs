using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Services.Token;
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
	private IConnectionMultiplexer _connectionMultiplexer = null!;
	private IDatabase _database = null!;
	private IBatch _batch = null!;
	private CachedUserSessionWriteRepository _repository = null!;

	private const int AccessTokenTtlMinutes = 15;

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

		_batch = Substitute.For<IBatch>();
		_batch.StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>(),
			flags: Arg.Any<CommandFlags>()
		).Returns(returnThis: true);

		_database = Substitute.For<IDatabase>();
		_database.CreateBatch(asyncState: Arg.Any<object>()).Returns(returnThis: _batch);

		_connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		_connectionMultiplexer.GetDatabase(
			db: Arg.Any<int>(),
			asyncState: Arg.Any<object>()
		).Returns(returnThis: _database);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions { InstanceName = "ft_test:" });

		RedisCache redisCache = new RedisCache(
			connectionMultiplexer: _connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);

		IOptionsMonitor<JwtOptions> jwtOptions = Substitute.For<IOptionsMonitor<JwtOptions>>();
		jwtOptions.CurrentValue.Returns(returnThis: new JwtOptions
		{
			Secret = new String(c: '0', count: 32),
			Issuer = "test",
			Audience = "test",
			AccessTokenTtlMinutes = AccessTokenTtlMinutes
		});

		_repository = new CachedUserSessionWriteRepository(
			inner: _inner,
			redisCache: redisCache,
			unitOfWork: _unitOfWork,
			jwtOptions: jwtOptions
		);
	}

	private List<RedisKey> CaptureWrittenKeys()
	{
		List<RedisKey> captured = [];
		_batch.StringSetAsync(
			key: Arg.Do<RedisKey>(useArgument: captured.Add),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>(),
			flags: Arg.Any<CommandFlags>()
		).Returns(returnThis: true);
		return captured;
	}

	private async Task SimulateCommitAsync()
	{
		foreach (Func<Task> callback in _committedCallbacks.OfType<Func<Task>>())
			await callback();
	}

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
		_database.DidNotReceive().CreateBatch(asyncState: Arg.Any<object>());
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
		_database.DidNotReceive().CreateBatch(asyncState: Arg.Any<object>());
	}

	[Test]
	public async Task RevokeAsync_WhenSessionWasRevoked_ShouldMarkItInRedisOnCommit()
	{
		Guid sessionId = Guid.CreateVersion7();
		_inner.RevokeAsync(
			sessionId: sessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [sessionId]);

		List<RedisKey> writtenKeys = CaptureWrittenKeys();

		await _repository.RevokeAsync(sessionId: sessionId, revokedAt: DateTimeOffset.UtcNow);
		await SimulateCommitAsync();

		await Assert.That(value: writtenKeys).Count().IsEqualTo(expected: 1);
		await Assert.That(value: (string)writtenKeys[0]!).IsEqualTo(expected: $"ft_test:revoked-session:{sessionId}");
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
		_database.DidNotReceive().CreateBatch(asyncState: Arg.Any<object>());
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
	public async Task RevokeAllExceptAsync_ShouldMarkEachRevokedSessionInRedisOnCommit()
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

		List<RedisKey> writtenKeys = CaptureWrittenKeys();

		await _repository.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: exceptSessionId,
			revokedAt: DateTimeOffset.UtcNow
		);
		await SimulateCommitAsync();

		await Assert.That(value: writtenKeys).Count().IsEqualTo(expected: 2);
		await Assert.That(value: writtenKeys.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:revoked-session:{revoked1}");
		await Assert.That(value: writtenKeys.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:revoked-session:{revoked2}");
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
		_database.DidNotReceive().CreateBatch(asyncState: Arg.Any<object>());
	}

	[Test]
	public async Task RevokeAllAsync_ShouldMarkEachRevokedSessionInRedisOnCommit()
	{
		Guid userId = Guid.CreateVersion7();
		Guid revoked1 = Guid.CreateVersion7();
		Guid revoked2 = Guid.CreateVersion7();

		_inner.RevokeAllAsync(
			userId: userId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [revoked1, revoked2]);

		List<RedisKey> writtenKeys = CaptureWrittenKeys();

		await _repository.RevokeAllAsync(userId: userId, revokedAt: DateTimeOffset.UtcNow);
		await SimulateCommitAsync();

		await Assert.That(value: writtenKeys).Count().IsEqualTo(expected: 2);
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
		_database.DidNotReceive().CreateBatch(asyncState: Arg.Any<object>());
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
		_database.DidNotReceive().CreateBatch(asyncState: Arg.Any<object>());
	}
}
