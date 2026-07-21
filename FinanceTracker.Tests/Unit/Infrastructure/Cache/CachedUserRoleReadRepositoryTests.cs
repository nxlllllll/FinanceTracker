using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedUserRoleReadRepositoryTests
{
	private IUserRoleReadRepository _inner = null!;
	private IDatabase _database = null!;
	private CachedUserRoleReadRepository _repository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<IUserRoleReadRepository>();
		IConnectionMultiplexer connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		_database = Substitute.For<IDatabase>();
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
		_repository = new CachedUserRoleReadRepository(inner: _inner, redisCache: redisCache);

		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: RedisValue.Null);
	}

	[Test]
	public async Task HasSystemRoleAsync_OnCacheMiss_CallsInnerAndCaches()
	{
		Guid userId = Guid.CreateVersion7();
		_inner.HasSystemRoleAsync(
			userId: userId,
			systemKey: SystemRole.Root,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		bool result = await _repository.HasSystemRoleAsync(userId: userId, systemKey: SystemRole.Root);

		await Assert.That(value: result).IsTrue();
		await _inner.Received(requiredNumberOfCalls: 1).HasSystemRoleAsync(
			userId: userId,
			systemKey: SystemRole.Root,
			ct: Arg.Any<CancellationToken>()
		);
		await _database.Received(requiredNumberOfCalls: 1).StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		);
	}

	[Test]
	public async Task HasSystemRoleAsync_OnCacheHit_DoesNotCallInner()
	{
		_database.StringGetAsync(
			key: Arg.Any<RedisKey>()
		).Returns(returnThis: (RedisValue)System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value: true));

		bool result = await _repository.HasSystemRoleAsync(userId: Guid.CreateVersion7(), systemKey: SystemRole.Root);

		await Assert.That(value: result).IsTrue();
		await _inner.DidNotReceive().HasSystemRoleAsync(
			userId: Arg.Any<Guid>(),
			systemKey: Arg.Any<SystemRole>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task KeyFor_ShouldIncludeUserIdAndSystemKey()
	{
		Guid userId = Guid.CreateVersion7();

		await Assert.That(value: CachedUserRoleReadRepository.KeyFor(userId: userId, systemKey: SystemRole.Admin))
			.IsEqualTo(expected: $"roles:{userId}:Admin");
	}
}
