using System.Text.Json;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedPermissionReadRepositoryTests
{
	private IUserPermissionReadRepository _inner = null!;
	private IConnectionMultiplexer _connectionMultiplexer = null!;
	private IDatabase _database = null!;
	private CachedUserPermissionReadRepository _repository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<IUserPermissionReadRepository>();
		_connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		_database = Substitute.For<IDatabase>();
		_connectionMultiplexer.GetDatabase(
			db: Arg.Any<int>(),
			asyncState: Arg.Any<object>()
		).Returns(returnThis: _database);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions
		{
			InstanceName = "ft_test:"
		});

		RedisCache redisCache = new RedisCache(
			connectionMultiplexer: _connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);
		_repository = new CachedUserPermissionReadRepository(inner: _inner, redisCache: redisCache);

		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(returnThis: RedisValue.Null);
	}

	[Test]
	public async Task GetPermissionsAsync_OnCacheMiss_CallsInner()
	{
		Guid userId = Guid.CreateVersion7();
		_inner.GetPermissionsAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new HashSet<string> { "account:write" });

		await _repository.GetPermissionsAsync(userId: userId);

		await _inner.Received(requiredNumberOfCalls: 1).GetPermissionsAsync(userId: userId, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetPermissionsAsync_OnCacheMiss_StoresResultInCache()
	{
		Guid userId = Guid.CreateVersion7();
		_inner.GetPermissionsAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new HashSet<string> { "account:write" });

		await _repository.GetPermissionsAsync(userId: userId);

		await _database.Received(requiredNumberOfCalls: 1).StringSetAsync(
			key: Arg.Any<RedisKey>(),
			value: Arg.Any<RedisValue>(),
			expiry: Arg.Any<Expiration>()
		);
	}

	[Test]
	public async Task GetPermissionsAsync_OnCacheHit_DoesNotCallInner()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(
			returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: new HashSet<string> { "account:write" })
		);

		await _repository.GetPermissionsAsync(userId: Guid.CreateVersion7());

		await _inner.DidNotReceive().GetPermissionsAsync(userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task GetPermissionsAsync_OnCacheHit_ReturnsCorrectValue()
	{
		_database.StringGetAsync(key: Arg.Any<RedisKey>()).Returns(
			returnThis: (RedisValue)JsonSerializer.SerializeToUtf8Bytes(value: new HashSet<string> { "balance:read" })
		);

		IReadOnlySet<string> result = await _repository.GetPermissionsAsync(userId: Guid.CreateVersion7());

		await Assert.That(value: result).Contains(expected: "balance:read");
	}

	[Test]
	public async Task GetPermissionsAsync_KeyFor_ShouldBeStablePerUser()
	{
		Guid userId = Guid.CreateVersion7();

		await Assert.That(value: CachedUserPermissionReadRepository.KeyFor(userId: userId)).IsEqualTo(expected: $"permissions:{userId}");
	}
}
