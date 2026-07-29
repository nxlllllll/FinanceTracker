using FinanceTracker.Core.Domains.User;
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
	private IConnectionMultiplexer _connectionMultiplexer = null!;
	private IDatabase _database = null!;
	private IBatch _batch = null!;
	private CachedUserSessionWriteRepository _repository = null!;

	private const int AccessTokenTtlMinutes = 15;

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<IUserSessionWriteRepository>();

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
		_database.DidNotReceive().CreateBatch(asyncState: Arg.Any<object>());
	}

	[Test]
	public async Task RevokeAsync_WhenSessionWasRevoked_ShouldMarkItInRedis()
	{
		Guid sessionId = Guid.CreateVersion7();
		_inner.RevokeAsync(
			sessionId: sessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IReadOnlyList<Guid>)[sessionId]);

		List<RedisKey> writtenKeys = CaptureWrittenKeys();

		await _repository.RevokeAsync(sessionId: sessionId, revokedAt: DateTimeOffset.UtcNow);

		await Assert.That(value: writtenKeys).Count().IsEqualTo(expected: 1);
		await Assert.That(value: (string)writtenKeys[0]!).IsEqualTo(expected: $"ft_test:revoked-session:{sessionId}");
	}

	[Test]
	public async Task RevokeAsync_WhenNothingWasRevoked_ShouldNotTouchRedis()
	{
		Guid sessionId = Guid.CreateVersion7();
		_inner.RevokeAsync(
			sessionId: sessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IReadOnlyList<Guid>)[]);

		await _repository.RevokeAsync(sessionId: sessionId, revokedAt: DateTimeOffset.UtcNow);

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
	public async Task RevokeAllExceptAsync_ShouldMarkEachRevokedSessionInRedis()
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
		).Returns(returnThis: (IReadOnlyList<Guid>)[revoked1, revoked2]);

		List<RedisKey> writtenKeys = CaptureWrittenKeys();

		await _repository.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: exceptSessionId,
			revokedAt: DateTimeOffset.UtcNow
		);

		await Assert.That(value: writtenKeys).Count().IsEqualTo(expected: 2);
		await Assert.That(value: writtenKeys.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:revoked-session:{revoked1}");
		await Assert.That(value: writtenKeys.Select(selector: k => (string)k!)).Contains(expected: $"ft_test:revoked-session:{revoked2}");
	}

	[Test]
	public async Task RevokeAllAsync_ShouldMarkEachRevokedSessionInRedis()
	{
		Guid userId = Guid.CreateVersion7();
		Guid revoked1 = Guid.CreateVersion7();
		Guid revoked2 = Guid.CreateVersion7();

		_inner.RevokeAllAsync(
			userId: userId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IReadOnlyList<Guid>)[revoked1, revoked2]);

		List<RedisKey> writtenKeys = CaptureWrittenKeys();

		await _repository.RevokeAllAsync(userId: userId, revokedAt: DateTimeOffset.UtcNow);

		await Assert.That(value: writtenKeys).Count().IsEqualTo(expected: 2);
	}

	[Test]
	public async Task RevokeAllAsync_WhenNothingWasRevoked_ShouldNotTouchRedis()
	{
		Guid userId = Guid.CreateVersion7();
		_inner.RevokeAllAsync(
			userId: userId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IReadOnlyList<Guid>)[]);

		await _repository.RevokeAllAsync(userId: userId, revokedAt: DateTimeOffset.UtcNow);

		_database.DidNotReceive().CreateBatch(asyncState: Arg.Any<object>());
	}
}
