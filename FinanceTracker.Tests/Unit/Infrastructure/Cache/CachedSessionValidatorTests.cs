using System.Text.Json;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Services.Token;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedSessionValidatorTests
{
	private IUserSessionReadRepository _sessionReadRepository = null!;
	private IDatabase _database = null!;
	private CachedSessionValidator _validator = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_sessionReadRepository = Substitute.For<IUserSessionReadRepository>();

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

		IOptionsMonitor<JwtOptions> jwtOptions = Substitute.For<IOptionsMonitor<JwtOptions>>();
		jwtOptions.CurrentValue.Returns(returnThis: new JwtOptions
		{
			Secret = new String(c: '0', count: 32),
			Issuer = "test",
			Audience = "test",
			ActiveSessionCacheSeconds = 60
		});

		_validator = new CachedSessionValidator(
			userSessionReadRepository: _sessionReadRepository,
			redisCache: redisCache,
			dateProvider: FakeDateProvider.Default,
			jwtOptions: jwtOptions
		);
	}

	private void CacheReturns(bool value) => _database.StringGetAsync(
		key: Arg.Any<RedisKey>(),
		flags: Arg.Any<CommandFlags>()
	).Returns(returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: value));

	private void CacheMisses() => _database.StringGetAsync(
		key: Arg.Any<RedisKey>(),
		flags: Arg.Any<CommandFlags>()
	).Returns(returnThis: RedisValue.Null);

	private void CacheIsDown() => _database.StringGetAsync(
		key: Arg.Any<RedisKey>(),
		flags: Arg.Any<CommandFlags>()
	).Returns<RedisValue>(returnThis: _ => throw new RedisConnectionException(
		failureType: ConnectionFailureType.UnableToConnect,
		message: "down",
		flags: CommandFlags.None
	));

	private void DatabaseSays(Guid sessionId, bool isActive) => _sessionReadRepository.IsActiveAsync(
		sessionId: sessionId,
		now: Arg.Any<DateTimeOffset>(),
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: isActive);

	private bool CacheWasWrittenFor(Guid sessionId)
	{
		return _database.ReceivedCalls().Where(predicate: call => call.GetMethodInfo().Name == nameof(IDatabase.StringSetAsync))
			.Select(selector: call => call.GetArguments())
			.Any(predicate: arguments => arguments.Length > 0 && arguments[0] is RedisKey key && (string)key! == $"ft_test:active-session:{sessionId}");
	}

	[Test]
	public async Task IsSessionActiveAsync_OnACacheHit_ShouldNotTouchTheDatabase()
	{
		Guid sessionId = Guid.CreateVersion7();
		CacheReturns(value: true);

		bool isActive = await _validator.IsSessionActiveAsync(sessionId: sessionId);

		await Assert.That(value: isActive).IsTrue();
		await _sessionReadRepository.DidNotReceive().IsActiveAsync(
			sessionId: Arg.Any<Guid>(),
			now: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task IsSessionActiveAsync_OnACachedNegative_ShouldStayNegativeWithoutTheDatabase()
	{
		Guid sessionId = Guid.CreateVersion7();
		CacheReturns(value: false);

		bool isActive = await _validator.IsSessionActiveAsync(sessionId: sessionId);

		await Assert.That(value: isActive).IsFalse();
		await _sessionReadRepository.DidNotReceive().IsActiveAsync(
			sessionId: Arg.Any<Guid>(),
			now: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task IsSessionActiveAsync_OnACacheMiss_ShouldFallBackToTheDatabase()
	{
		Guid sessionId = Guid.CreateVersion7();
		CacheMisses();
		DatabaseSays(sessionId: sessionId, isActive: true);

		bool isActive = await _validator.IsSessionActiveAsync(sessionId: sessionId);

		await Assert.That(value: isActive).IsTrue();
		await _sessionReadRepository.Received(requiredNumberOfCalls: 1).IsActiveAsync(
			sessionId: sessionId,
			now: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task IsSessionActiveAsync_WhenRedisIsUnavailable_ShouldFallBackToTheDatabase()
	{
		Guid sessionId = Guid.CreateVersion7();
		CacheIsDown();
		DatabaseSays(sessionId: sessionId, isActive: false);

		bool isActive = await _validator.IsSessionActiveAsync(sessionId: sessionId);

		await Assert.That(value: isActive).IsFalse();
		await _sessionReadRepository.Received(requiredNumberOfCalls: 1).IsActiveAsync(
			sessionId: sessionId,
			now: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task IsSessionActiveAsync_WhenARevokedSessionIsMissingFromTheCache_ShouldRejectIt()
	{
		Guid sessionId = Guid.CreateVersion7();
		CacheMisses();
		DatabaseSays(sessionId: sessionId, isActive: false);

		bool isActive = await _validator.IsSessionActiveAsync(sessionId: sessionId);

		await Assert.That(value: isActive).IsFalse();
	}

	[Test]
	public async Task IsSessionActiveAsync_AfterConsultingTheDatabase_ShouldCacheTheAnswer()
	{
		Guid sessionId = Guid.CreateVersion7();
		CacheMisses();
		DatabaseSays(sessionId: sessionId, isActive: true);

		await _validator.IsSessionActiveAsync(sessionId: sessionId);

		await Assert.That(value: CacheWasWrittenFor(sessionId: sessionId)).IsTrue();
	}

	[Test]
	public async Task IsSessionActiveAsync_WhenTheDatabaseFails_ShouldPropagate()
	{
		Guid sessionId = Guid.CreateVersion7();
		CacheMisses();
		_sessionReadRepository.IsActiveAsync(
			sessionId: sessionId,
			now: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(ex: new InvalidOperationException(message: "database unavailable"));

		await Assert.That(action: async () => await _validator.IsSessionActiveAsync(sessionId: sessionId)).Throws<InvalidOperationException>();
	}
}
