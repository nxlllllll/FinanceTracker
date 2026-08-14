using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using UserRoleAggregate = FinanceTracker.Core.Domains.UserRole.UserRole;

namespace FinanceTracker.Tests.Unit.Infrastructure.Cache;

public sealed class CachedUserRoleRepositoryTests
{
	private const string InstanceName = "ft_test:";

	private IUserRoleRepository _inner = null!;
	private IPermissionSourceReadRepository _permissionSources = null!;
	private IDatabase _database = null!;
	private List<Func<Task>?> _committedCallbacks = null!;
	private CachedUserRoleRepository _repository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_inner = Substitute.For<IUserRoleRepository>();

		_permissionSources = Substitute.For<IPermissionSourceReadRepository>();
		_permissionSources.GetDirectGrantsAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new HashSet<string>());
		_permissionSources.GetPermissionsForRolesAsync(
			roleIds: Arg.Any<IReadOnlyCollection<Guid>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new HashSet<string>());
		_permissionSources.GetSystemRolesAsync(
			roleIds: Arg.Any<IReadOnlyCollection<Guid>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new HashSet<SystemRole>());

		_database = Substitute.For<IDatabase>();

		IConnectionMultiplexer connectionMultiplexer = Substitute.For<IConnectionMultiplexer>();
		connectionMultiplexer.GetDatabase(
			db: Arg.Any<int>(),
			asyncState: Arg.Any<object>()
		).Returns(returnThis: _database);

		IOptionsMonitor<RedisOptions> redisOptions = Substitute.For<IOptionsMonitor<RedisOptions>>();
		redisOptions.CurrentValue.Returns(returnThis: new RedisOptions { InstanceName = InstanceName });

		RedisCache redisCache = new RedisCache(
			connectionMultiplexer: connectionMultiplexer,
			options: redisOptions,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<RedisCache>.Instance
		);

		_committedCallbacks = [];
		IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWork.When(
			substituteCall: uow => uow.OnCommitted(callback: Arg.Any<Func<Task>>())
		).Do(
			callbackWithArguments: call => _committedCallbacks.Add(item: call.Arg<Func<Task>>())
		);

		_repository = new CachedUserRoleRepository(
			inner: _inner,
			permissionSources: _permissionSources,
			redisCache: redisCache,
			unitOfWork: unitOfWork,
			logger: NullLogger<CachedUserRoleRepository>.Instance
		);
	}

	private async Task CommitAsync()
	{
		foreach (Func<Task>? callback in _committedCallbacks)
			await callback!.Invoke();

		_committedCallbacks.Clear();
	}

	private Dictionary<string, string> CapturedWrites()
	{
		return _database.ReceivedCalls()
			.Where(predicate: call => call.GetMethodInfo().Name == nameof(IDatabase.StringSetAsync))
			.Select(selector: call => call.GetArguments())
			.ToDictionary(
				keySelector: arguments => ((RedisKey)arguments[0]!).ToString(),
				elementSelector: arguments => System.Text.Encoding.UTF8.GetString(bytes: ((byte[])(RedisValue)arguments[1]!)!)
			);
	}

	private static UserRoleAggregate WithRoles(Guid userId, params Guid[] roleIds)
	{
		UserRoleAggregate userRole = UserRoleAggregate.Create(
			occurredAt: FakeDateProvider.Default.UtcNow,
			userId: userId
		).Value!;

		foreach (Guid roleId in roleIds)
		{
			userRole.Assign(
				occurredAt: FakeDateProvider.Default.UtcNow,
				roleId: roleId,
				assignedBy: userId
			);
		}

		return userRole;
	}

	private static string RootKey(Guid userId)
		=> $"{InstanceName}{PermissionCacheKeys.SystemRoleKey(userId: userId, systemKey: SystemRole.Root)}";

	[Test]
	public async Task SaveAsync_ShouldWriteFalseForASystemRoleTheUserNoLongerHolds()
	{
		Guid userId = Guid.CreateVersion7();

		await _repository.SaveAsync(userRole: WithRoles(userId: userId, Guid.CreateVersion7()));
		await CommitAsync();

		Dictionary<string, string> writes = CapturedWrites();

		await Assert.That(value: writes).ContainsKey(expectedKey: RootKey(userId: userId))
			.Because(message: "removing a role must overwrite the root entry, not leave it to expire");

		await Assert.That(value: writes[RootKey(userId: userId)]).IsEqualTo(expected: "false")
			.Because(message: "a user who does not hold root must not be answered as root from cache");
	}

	[Test]
	public async Task SaveAsync_ShouldWriteTrueForAHeldSystemRole()
	{
		Guid userId = Guid.CreateVersion7();
		Guid rootRoleId = Guid.CreateVersion7();

		_permissionSources.GetSystemRolesAsync(
			roleIds: Arg.Any<IReadOnlyCollection<Guid>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new HashSet<SystemRole> { SystemRole.Root });

		await _repository.SaveAsync(userRole: WithRoles(userId: userId, rootRoleId));
		await CommitAsync();

		await Assert.That(value: CapturedWrites()[RootKey(userId: userId)]).IsEqualTo(expected: "true");
	}

	[Test]
	public async Task SaveAsync_ShouldWriteAnEntryForEverySystemRole()
	{
		Guid userId = Guid.CreateVersion7();

		await _repository.SaveAsync(userRole: WithRoles(userId: userId, Guid.CreateVersion7()));
		await CommitAsync();

		Dictionary<string, string> writes = CapturedWrites();

		foreach (SystemRole systemRole in Enum.GetValues<SystemRole>())
		{
			string key = $"{InstanceName}{PermissionCacheKeys.SystemRoleKey(userId: userId, systemKey: systemRole)}";

			await Assert.That(value: writes).ContainsKey(expectedKey: key)
				.Because(message: $"a system role added later must be covered without touching the decorator; '{systemRole}' was not written");
		}
	}

	[Test]
	public async Task SaveAsync_ShouldStillRefreshThePermissionSet()
	{
		Guid userId = Guid.CreateVersion7();

		await _repository.SaveAsync(userRole: WithRoles(userId: userId, Guid.CreateVersion7()));
		await CommitAsync();

		string key = $"{InstanceName}{PermissionCacheKeys.Permissions(userId: userId)}";

		await Assert.That(value: CapturedWrites()).ContainsKey(expectedKey: key)
			.Because(message: "the existing permission refresh must survive the role-cache fix");
	}

	[Test]
	public async Task SaveAsync_ShouldNotTouchTheCacheBeforeTheTransactionCommits()
	{
		Guid userId = Guid.CreateVersion7();

		await _repository.SaveAsync(userRole: WithRoles(userId: userId, Guid.CreateVersion7()));

		await Assert.That(value: CapturedWrites()).IsEmpty()
			.Because(message: "a cache written before commit would publish a membership change that may still roll back");
	}
}
