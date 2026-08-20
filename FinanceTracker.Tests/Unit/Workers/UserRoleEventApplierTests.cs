using FinanceTracker.Contracts.Events.UserRole;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.UserRoleProjection.Projection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class UserRoleEventApplierTests
{
	private sealed record UnrelatedFakeEvent : Contracts.Events.Abstraction.IIntegrationEvent
	{
		public Guid EventId => Guid.CreateVersion7();
		public Guid AggregateId => Guid.CreateVersion7();
		public int Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
	}

	private IUserRoleWriteRepository _repository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IDatabase _database = null!;
	private List<Func<Task>> _committedCallbacks = null!;
	private UserRoleEventApplier _applier = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_repository = Substitute.For<IUserRoleWriteRepository>();

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

		_committedCallbacks = [];
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.OnCommitted(callback: Arg.Do<Func<Task>>(useArgument: callback => _committedCallbacks.Add(item: callback)));

		_applier = new UserRoleEventApplier(
			repository: _repository,
			redisCache: redisCache,
			unitOfWork: _unitOfWork
		);
	}

	private async Task CommitAsync()
	{
		foreach (Func<Task> callback in _committedCallbacks)
			await callback();

		_committedCallbacks.Clear();
	}

	private static string ExpectedKeyFor(Guid userId)
		=> $"ft_test:{PermissionCacheKeys.Permissions(userId: userId)}";

	[Test]
	public async Task ApplyAsync_WithUserRoleCreatedEvent_ShouldDoNothing()
	{
		await _applier.ApplyAsync(@event: new UserRoleCreatedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: Guid.CreateVersion7(),
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await _repository.DidNotReceive().AssignAsync(@event: Arg.Any<RoleAssigned>(), ct: Arg.Any<CancellationToken>());
		await _repository.DidNotReceive().RemoveAsync(@event: Arg.Any<RoleRemoved>(), ct: Arg.Any<CancellationToken>());
		await Assert.That(value: _committedCallbacks).IsEmpty().Because(message: """
			No membership changed, so there is nothing to evict. Scheduling a callback anyway would
			be dead work on every user's very first role assignment.
		""");
	}

	[Test]
	public async Task ApplyAsync_WithRoleAssignedEvent_ShouldForwardTheVersionToTheRepository()
	{
		Guid userId = Guid.CreateVersion7();
		Guid roleId = Guid.CreateVersion7();
		Guid assignedBy = Guid.CreateVersion7();

		await _applier.ApplyAsync(@event: new RoleAssignedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: roleId,
			AssignedBy: assignedBy,
			Version: 4,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await _repository.Received(requiredNumberOfCalls: 1).AssignAsync(
			@event: Arg.Is<RoleAssigned>(predicate: e =>
				e!.UserId == userId &&
				e.RoleId == roleId &&
				e.AssignedBy == assignedBy &&
				e.Version == 4
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_WithRoleRemovedEvent_ShouldForwardTheVersionToTheRepository()
	{
		Guid userId = Guid.CreateVersion7();
		Guid roleId = Guid.CreateVersion7();
		Guid removedBy = Guid.CreateVersion7();

		await _applier.ApplyAsync(@event: new RoleRemovedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: roleId,
			RemovedBy: removedBy,
			Version: 5,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await _repository.Received(requiredNumberOfCalls: 1).RemoveAsync(
			@event: Arg.Is<RoleRemoved>(predicate: e =>
				e!.UserId == userId &&
				e.RoleId == roleId &&
				e.RemovedBy == removedBy &&
				e.Version == 5
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_WithMembershipChange_ShouldNotTouchTheCacheBeforeTheCommit()
	{
		Guid userId = Guid.CreateVersion7();

		await _applier.ApplyAsync(@event: new RoleAssignedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: Guid.CreateVersion7(),
			AssignedBy: Guid.CreateVersion7(),
			Version: 2,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await _database.DidNotReceive().KeyDeleteAsync(keys: Arg.Any<RedisKey[]>());
		await Assert.That(value: _committedCallbacks.Count).IsEqualTo(expected: 1).Because(message: """
			Evicting inline drops the key while this projection's own UPDATE is still uncommitted.
			A concurrent authorization check landing in that window reads the pre-update read model
			and caches the stale permission set for the full TTL — replacing the correct value the
			write side had already put there. The eviction has to be queued for after the commit.
		""");
	}

	[Test]
	public async Task ApplyAsync_WithRoleAssignedEvent_ShouldInvalidateThePermissionCacheOnceCommitted()
	{
		Guid userId = Guid.CreateVersion7();

		RedisKey[]? deletedKeys = null;
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deletedKeys = k)).Returns(returnThis: 1L);

		await _applier.ApplyAsync(@event: new RoleAssignedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: Guid.CreateVersion7(),
			AssignedBy: Guid.CreateVersion7(),
			Version: 2,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await CommitAsync();

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: (string)deletedKeys![0]!).IsEqualTo(expected: ExpectedKeyFor(userId: userId)).Because(message: """
			Roles feed effective permissions, so a membership change has to drop the same cache entry
			a grant would. Leaving it in place means the change quietly does nothing until it expires.
		""");
	}

	[Test]
	public async Task ApplyAsync_WithRoleRemovedEvent_ShouldInvalidateThePermissionCacheOnceCommitted()
	{
		Guid userId = Guid.CreateVersion7();

		RedisKey[]? deletedKeys = null;
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deletedKeys = k)).Returns(returnThis: 1L);

		await _applier.ApplyAsync(@event: new RoleRemovedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			RoleId: Guid.CreateVersion7(),
			RemovedBy: Guid.CreateVersion7(),
			Version: 3,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await CommitAsync();

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: (string)deletedKeys![0]!).IsEqualTo(expected: ExpectedKeyFor(userId: userId)).Because(message: """
			Losing a role has to drop the same entry gaining one does. Skipping it here leaves the
			permissions the removed role granted in effect until the TTL expires.
		""");
	}

	[Test]
	public async Task ApplyAsync_WithUnknownEventType_ShouldThrowUnknownEventException()
	{
		await Assert.That(action: async () => await _applier.ApplyAsync(@event: new UnrelatedFakeEvent())).Throws<UnknownEventException>();
	}
}
