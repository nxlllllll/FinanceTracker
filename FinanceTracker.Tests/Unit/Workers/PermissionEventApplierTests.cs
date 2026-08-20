using FinanceTracker.Contracts.Events.UserPermission;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.PermissionProjection.Projection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class PermissionEventApplierTests
{
	private sealed record UnrelatedFakeEvent : Contracts.Events.Abstraction.IIntegrationEvent
	{
		public Guid EventId => Guid.CreateVersion7();
		public Guid AggregateId => Guid.CreateVersion7();
		public int Version => 1;
		public DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
	}

	private IUserPermissionWriteRepository _repository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IDatabase _database = null!;
	private List<Func<Task>> _committedCallbacks = null!;
	private PermissionEventApplier _applier = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_repository = Substitute.For<IUserPermissionWriteRepository>();

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

		_applier = new PermissionEventApplier(
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
	public async Task ApplyAsync_WithUserPermissionCreatedEvent_ShouldBeANoOp()
	{
		Guid userId = Guid.CreateVersion7();

		await _applier.ApplyAsync(@event: new UserPermissionCreatedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await _repository.DidNotReceive().GrantAsync(
			@event: Arg.Any<PermissionGranted>(),
			ct: Arg.Any<CancellationToken>()
		);
		await Assert.That(value: _committedCallbacks).IsEmpty().Because(message: """
			There is no header row per user, so nothing changed and nothing needs evicting.
			Scheduling a callback here would be dead work on every user's very first grant.
		""");

		await CommitAsync();

		await _database.DidNotReceive().KeyDeleteAsync(keys: Arg.Any<RedisKey[]>());
	}

	[Test]
	public async Task ApplyAsync_WithPermissionGrantedEvent_ShouldCallGrantAsync()
	{
		Guid userId = Guid.CreateVersion7();
		Guid grantedBy = Guid.CreateVersion7();

		await _applier.ApplyAsync(@event: new PermissionGrantedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			GrantedBy: grantedBy,
			Permission: "account:write",
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await _repository.Received(requiredNumberOfCalls: 1).GrantAsync(
			@event: Arg.Is<PermissionGranted>(e => e!.UserId == userId && e.GrantedBy == grantedBy && e.Permission == "account:write"),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_WithPermissionGrantedEvent_ShouldNotTouchTheCacheBeforeTheCommit()
	{
		Guid userId = Guid.CreateVersion7();

		await _applier.ApplyAsync(@event: new PermissionGrantedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			GrantedBy: Guid.CreateVersion7(),
			Permission: "account:write",
			Version: 1,
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
	public async Task ApplyAsync_WithPermissionGrantedEvent_ShouldInvalidatePermissionCacheOnceCommitted()
	{
		Guid userId = Guid.CreateVersion7();

		RedisKey[]? deletedKeys = null;
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deletedKeys = k)).Returns(returnThis: 1L);

		await _applier.ApplyAsync(@event: new PermissionGrantedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			GrantedBy: Guid.CreateVersion7(),
			Permission: "account:write",
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await CommitAsync();

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: deletedKeys!.Length).IsEqualTo(expected: 1);
		await Assert.That(value: (string)deletedKeys![0]!).IsEqualTo(expected: ExpectedKeyFor(userId: userId));
	}

	[Test]
	public async Task ApplyAsync_WithPermissionRevokedEvent_ShouldCallRevokeAsync()
	{
		Guid userId = Guid.CreateVersion7();

		await _applier.ApplyAsync(@event: new PermissionRevokedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			RevokedBy: Guid.CreateVersion7(),
			Permission: "account:write",
			Version: 2,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await _repository.Received(requiredNumberOfCalls: 1).RevokeAsync(
			@event: Arg.Is<PermissionRevoked>(predicate: e => e!.UserId == userId && e.Permission == "account:write" && e.Version == 2),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_WithPermissionRevokedEvent_ShouldNotTouchTheCacheBeforeTheCommit()
	{
		Guid userId = Guid.CreateVersion7();

		await _applier.ApplyAsync(@event: new PermissionRevokedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			RevokedBy: Guid.CreateVersion7(),
			Permission: "account:write",
			Version: 2,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await _database.DidNotReceive().KeyDeleteAsync(keys: Arg.Any<RedisKey[]>());
		await Assert.That(value: _committedCallbacks.Count).IsEqualTo(expected: 1).Because(message: """
			A revoke evicted inline is the worst case of the same race: the stale set cached during
			the window still contains the permission that was just taken away, and it stays in
			effect until the TTL expires.
		""");
	}

	[Test]
	public async Task ApplyAsync_WithPermissionRevokedEvent_ShouldInvalidatePermissionCacheOnceCommitted()
	{
		Guid userId = Guid.CreateVersion7();

		RedisKey[]? deletedKeys = null;
		_database.KeyDeleteAsync(keys: Arg.Do<RedisKey[]>(useArgument: k => deletedKeys = k)).Returns(returnThis: 1L);

		await _applier.ApplyAsync(@event: new PermissionRevokedEvent(
			EventId: Guid.CreateVersion7(),
			UserId: userId,
			RevokedBy: Guid.CreateVersion7(),
			Permission: "account:write",
			Version: 2,
			OccurredAt: FakeDateProvider.Default.UtcNow
		));

		await CommitAsync();

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: (string)deletedKeys![0]!).IsEqualTo(expected: ExpectedKeyFor(userId: userId));
	}

	[Test]
	public async Task ApplyAsync_WithUnknownEventType_ShouldThrowUnknownEventException()
	{
		await Assert.That(
			action: async () => await _applier.ApplyAsync(@event: new UnrelatedFakeEvent())
		).Throws<UnknownEventException>();
	}
}
