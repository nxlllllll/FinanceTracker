using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Services.EventStore;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.UserPermission;

public sealed class UserPermissionRepositoryTests : DatabaseFixture
{
	private UserPermissionRepository _repository = null!;
	private EFUnitOfWork _unitOfWork = null!;

	private PostgresEventStore CreateEventStore() => new PostgresEventStore(
		context: Context,
		eventTypeResolver: new EventTypeResolver(
			assembly: typeof(FinanceTracker.Core.Domains.Abstractions.EventStore.Event.IEvent).Assembly,
			logger: Substitute.For<ILogger<EventTypeResolver>>()
		),
		integrationEventMapper: new UserPermissionIntegrationEventMapper(),
		integrationEventTypeResolver: new IntegrationEventTypeResolver(
			contractsAssembly: typeof(IIntegrationEvent).Assembly,
			logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
		),
		dateProvider: FakeDateProvider.Default,
		correlationContext: Substitute.For<ICorrelationContext>(),
		upcasterRegistry: CreatePassthroughUpcasterRegistry(),
		options: new FakeOptionsMonitor<EventStoreOptions>(value: new EventStoreOptions()),
		logger: Substitute.For<ILogger<PostgresEventStore>>(),
		eventSchemaHealthState: Substitute.For<IEventSchemaHealthState>()
	);

	private static IEventUpcasterRegistry CreatePassthroughUpcasterRegistry()
	{
		IEventUpcasterRegistry registry = Substitute.For<IEventUpcasterRegistry>();
		registry.HasChain(eventType: Arg.Any<string>()).Returns(returnThis: false);
		return registry;
	}

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_unitOfWork = new EFUnitOfWork(context: Context, logger: Substitute.For<ILogger<EFUnitOfWork>>());
		_repository = new UserPermissionRepository(
			eventStore: CreateEventStore(),
			unitOfWork: _unitOfWork
		);
	}

	[After(hookType: Test)]
	public async Task TearDownAsync()
		=> await _unitOfWork.DisposeAsync();

	private Task SaveAsync(Core.Domains.UserPermission.UserPermission userPermission) => _unitOfWork.ExecuteInTransactionAsync(
		operation: async () => await _repository.SaveAsync(userPermission: userPermission, ct: CancellationToken.None)
	);

	[Test]
	public async Task GetByUserIdAsync_WhenNotExists_ShouldReturnNull()
	{
		Core.Domains.UserPermission.UserPermission? result = await _repository.GetByUserIdAsync(
			userId: Guid.CreateVersion7(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByUserIdAsync_AfterCreate_ShouldReturnEmptyPermissionSet()
	{
		Core.Domains.UserPermission.UserPermission userPermission = UserPermissionFactory.Create();
		await SaveAsync(userPermission: userPermission);

		Core.Domains.UserPermission.UserPermission? restored = await _repository.GetByUserIdAsync(
			userId: userPermission.UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: restored).IsNotNull();
		await Assert.That(value: restored!.Id).IsEqualTo(expected: userPermission.UserId);
		await Assert.That(value: restored.Permissions).IsEmpty();
	}

	[Test]
	public async Task GetByUserIdAsync_AfterGrant_ShouldReturnGrantedPermission()
	{
		Core.Domains.UserPermission.UserPermission userPermission = UserPermissionFactory.Create();
		await SaveAsync(userPermission: userPermission);

		Core.Domains.UserPermission.UserPermission? loaded = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);
		Permission permission = Permission.Create(resource: Resource.Account, action: PermissionAction.Write).Value!;
		loaded!.Grant(occurredAt: FakeDateProvider.Default.UtcNow, grantedBy: Guid.CreateVersion7(), permission: permission);
		await SaveAsync(userPermission: loaded);

		Core.Domains.UserPermission.UserPermission? restored = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);

		await Assert.That(value: restored!.Permissions).Contains(expected: "account:write");
	}

	[Test]
	public async Task GetByUserIdAsync_AfterGrantThenRevoke_ShouldReturnEmptySet()
	{
		Core.Domains.UserPermission.UserPermission userPermission = UserPermissionFactory.Create();
		await SaveAsync(userPermission: userPermission);

		Core.Domains.UserPermission.UserPermission? loaded = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);
		Permission permission = Permission.Create(resource: Resource.Transaction, action: PermissionAction.Delete).Value!;
		loaded!.Grant(occurredAt: FakeDateProvider.Default.UtcNow, grantedBy: Guid.CreateVersion7(), permission: permission);
		await SaveAsync(userPermission: loaded);

		Core.Domains.UserPermission.UserPermission? afterGrant = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);
		afterGrant!.Revoke(occurredAt: FakeDateProvider.Default.UtcNow, revokedBy: Guid.CreateVersion7(), permission: permission);
		await SaveAsync(userPermission: afterGrant);

		Core.Domains.UserPermission.UserPermission? restored = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);

		await Assert.That(value: restored!.Permissions).IsEmpty();
	}

	[Test]
	public async Task GetByUserIdAsync_AfterMultipleGrants_ShouldReturnAllPermissions()
	{
		Core.Domains.UserPermission.UserPermission userPermission = UserPermissionFactory.Create();
		await SaveAsync(userPermission: userPermission);

		Core.Domains.UserPermission.UserPermission? loaded = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);
		loaded!.Grant(
			occurredAt: FakeDateProvider.Default.UtcNow,
			grantedBy: Guid.CreateVersion7(),
			permission: Permission.Create(resource: Resource.Account, action: PermissionAction.Read).Value!
		);
		loaded.Grant(
			occurredAt: FakeDateProvider.Default.UtcNow,
			grantedBy: Guid.CreateVersion7(),
			permission: Permission.Create(resource: Resource.Balance, action: PermissionAction.Read).Value!
		);
		await SaveAsync(userPermission: loaded);

		Core.Domains.UserPermission.UserPermission? restored = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);

		await Assert.That(value: restored!.Permissions).Count().IsEqualTo(expected: 2);
		await Assert.That(value: restored.Permissions).Contains(expected: "account:read");
		await Assert.That(value: restored.Permissions).Contains(expected: "balance:read");
	}

	[Test]
	public async Task SaveAsync_WithNoEvents_ShouldNotThrowAndNotPersistAnything()
	{
		Core.Domains.UserPermission.UserPermission userPermission = UserPermissionFactory.CreateWithGrant();
		userPermission.ClearEvents();

		await Assert.That(action: async () => await SaveAsync(userPermission: userPermission)).ThrowsNothing();

		Core.Domains.UserPermission.UserPermission? loaded = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);
		await Assert.That(value: loaded).IsNull();
	}

	[Test]
	public async Task SaveAsync_ThenSaveAgain_ShouldAccumulateVersionCorrectly()
	{
		Core.Domains.UserPermission.UserPermission userPermission = UserPermissionFactory.Create();
		await SaveAsync(userPermission: userPermission);
		int versionAfterCreate = userPermission.Version;

		Core.Domains.UserPermission.UserPermission? loaded = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);
		loaded!.Grant(occurredAt: FakeDateProvider.Default.UtcNow, grantedBy: Guid.CreateVersion7(), permission: Permission.Create(resource: Resource.Category, action: PermissionAction.Read).Value!);
		await SaveAsync(userPermission: loaded);

		Core.Domains.UserPermission.UserPermission? restored = await _repository.GetByUserIdAsync(userId: userPermission.UserId, ct: CancellationToken.None);

		await Assert.That(value: restored!.Version).IsGreaterThan(minimum: versionAfterCreate);
	}
}
