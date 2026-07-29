using FinanceTracker.Contracts.Events.UserPermission;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
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
	private IDatabase _database = null!;
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

		_applier = new PermissionEventApplier(repository: _repository, redisCache: redisCache);
	}

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
	public async Task ApplyAsync_WithPermissionGrantedEvent_ShouldInvalidatePermissionCacheForThatUser()
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

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: deletedKeys!.Length).IsEqualTo(expected: 1);
		await Assert.That(value: (string)deletedKeys![0]!).IsEqualTo(expected: $"ft_test:{CachedUserPermissionReadRepository.KeyFor(userId: userId)}");
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
			userId: userId,
			permission: "account:write",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_WithPermissionRevokedEvent_ShouldInvalidatePermissionCacheForThatUser()
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

		await Assert.That(value: deletedKeys).IsNotNull();
		await Assert.That(value: (string)deletedKeys![0]!).IsEqualTo(expected: $"ft_test:{CachedUserPermissionReadRepository.KeyFor(userId: userId)}");
	}

	[Test]
	public async Task ApplyAsync_WithUnknownEventType_ShouldThrowUnknownEventException()
	{
		await Assert.That(
			action: async () => await _applier.ApplyAsync(@event: new UnrelatedFakeEvent())
		).Throws<UnknownEventException>();
	}
}
