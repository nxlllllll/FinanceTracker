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

public sealed class CachedRoleRepositoryTests
{
	private IRoleRepository _inner = null!;
	private IDatabase _database = null!;
	private CachedRoleRepository _repository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<IRoleRepository>();

		_database = Substitute.For<IDatabase>();
		_database.KeyDeleteAsync(keys: Arg.Any<RedisKey[]>()).Returns(returnThis: 1L);

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

		_repository = new CachedRoleRepository(inner: _inner, redisCache: redisCache);
	}

	private void ReturnsMembers(
		Guid roleId,
		params Guid[] userIds
	) => _inner.GetMemberUserIdsAsync(
		roleId: roleId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: [..userIds]);

	[Test]
	public async Task ReplacePermissionsAsync_ShouldInvalidateEveryMemberPermissionCache()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid firstMember = Guid.CreateVersion7();
		Guid secondMember = Guid.CreateVersion7();
		ReturnsMembers(roleId: roleId, firstMember, secondMember);

		RedisKey[] deleted = [];
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deleted = k)).Returns(returnThis: 1L);

		await _repository.ReplacePermissionsAsync(roleId: roleId, permissions: new HashSet<Permission>
		{
			Permission.Create(resource: Resource.Category, action: PermissionAction.Read).Value!
		});

		List<string> keys = deleted.Select(selector: k => (string)k!).ToList();

		await Assert.That(value: keys).Contains(expected: $"ft_test:{CachedUserPermissionReadRepository.KeyFor(userId: firstMember)}").Because(message: """
			Changing a role changes what its members can do straight away, and their cached permission
			sets are what the API actually reads. Leaving them in place means the change appears to do
			nothing for as long as the entries live.
		""");
		await Assert.That(value: keys).Contains(expected: $"ft_test:{CachedUserPermissionReadRepository.KeyFor(userId: secondMember)}");
	}

	[Test]
	public async Task DeleteAsync_ShouldInvalidateEveryMemberPermissionCache()
	{
		Guid roleId = Guid.CreateVersion7();
		Guid member = Guid.CreateVersion7();
		ReturnsMembers(roleId: roleId, member);

		RedisKey[] deleted = [];
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deleted = k)).Returns(returnThis: 1L);

		await _repository.DeleteAsync(roleId: roleId);

		List<string> keys = deleted.Select(selector: k => (string)k!).ToList();
		await Assert.That(value: keys).Contains(expected: $"ft_test:{CachedUserPermissionReadRepository.KeyFor(userId: member)}");
	}
}
