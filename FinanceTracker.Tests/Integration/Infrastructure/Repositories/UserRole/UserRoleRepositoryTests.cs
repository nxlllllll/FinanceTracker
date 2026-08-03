using System.Text.Json;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.UserRole;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.UserRole;

public sealed class UserRoleRepositoryTests : DatabaseFixture
{
	private UserRoleRepository _repository = null!;
	private EFUnitOfWork _unitOfWork = null!;

	private PostgresEventStore CreateEventStore() => new PostgresEventStore(
		context: Context,
		eventTypeResolver: new EventTypeResolver(
			assembly: typeof(IEvent).Assembly,
			logger: Substitute.For<ILogger<EventTypeResolver>>()
		),
		integrationEventMapper: new UserRoleIntegrationEventMapper(),
		integrationEventTypeResolver: new IntegrationEventTypeResolver(
			contractsAssembly: typeof(IIntegrationEvent).Assembly,
			logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
		),
		dateProvider: FakeDateProvider.Default,
		correlationContext: Substitute.For<ICorrelationContext>(),
		upcasterRegistry: CreatePassthroughUpcasterRegistry(),
		options: new FakeOptionsMonitor<EventStoreOptions>(value: new EventStoreOptions()),
		logger: Substitute.For<ILogger<PostgresEventStore>>()
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
		_repository = new UserRoleRepository(
			eventStore: CreateEventStore(),
			unitOfWork: _unitOfWork
		);
	}

	[After(hookType: Test)]
	public async Task TearDownAsync()
		=> await _unitOfWork.DisposeAsync();

	private Task SaveAsync(Core.Domains.UserRole.UserRole userRole) => _unitOfWork.ExecuteInTransactionAsync(
		operation: async () => await _repository.SaveAsync(userRole: userRole, ct: CancellationToken.None)
	);

	[Test]
	public async Task GetByUserIdAsync_WhenNotExists_ShouldReturnNull()
	{
		Core.Domains.UserRole.UserRole? result = await _repository.GetByUserIdAsync(
			userId: Guid.CreateVersion7(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByUserIdAsync_AfterCreate_ShouldReturnEmptyMembership()
	{
		Core.Domains.UserRole.UserRole userRole = UserRoleFactory.Create();
		await SaveAsync(userRole: userRole);

		Core.Domains.UserRole.UserRole? restored = await _repository.GetByUserIdAsync(
			userId: userRole.UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: restored).IsNotNull();
		await Assert.That(value: restored!.Id).IsEqualTo(expected: userRole.UserId);
		await Assert.That(value: restored.RoleIds).IsEmpty();
	}

	[Test]
	public async Task GetByUserIdAsync_AfterAssign_ShouldReturnTheRole()
	{
		Guid roleId = Guid.CreateVersion7();
		Core.Domains.UserRole.UserRole userRole = UserRoleFactory.Create();
		await SaveAsync(userRole: userRole);

		Core.Domains.UserRole.UserRole? loaded = await _repository.GetByUserIdAsync(userId: userRole.UserId, ct: CancellationToken.None);
		loaded!.Assign(
			occurredAt: FakeDateProvider.Default.UtcNow,
			roleId: roleId,
			assignedBy: Guid.CreateVersion7()
		);
		await SaveAsync(userRole: loaded);

		Core.Domains.UserRole.UserRole? restored = await _repository.GetByUserIdAsync(userId: userRole.UserId, ct: CancellationToken.None);

		await Assert.That(value: restored!.RoleIds).Contains(expected: roleId);
	}

	[Test]
	public async Task GetByUserIdAsync_AfterAssignThenRemove_ShouldReturnEmptyMembership()
	{
		Guid roleId = Guid.CreateVersion7();
		Core.Domains.UserRole.UserRole userRole = UserRoleFactory.Create();
		await SaveAsync(userRole: userRole);

		Core.Domains.UserRole.UserRole? loaded = await _repository.GetByUserIdAsync(userId: userRole.UserId, ct: CancellationToken.None);
		loaded!.Assign(
			occurredAt: FakeDateProvider.Default.UtcNow,
			roleId: roleId,
			assignedBy: Guid.CreateVersion7()
		);
		await SaveAsync(userRole: loaded);

		Core.Domains.UserRole.UserRole? afterAssign = await _repository.GetByUserIdAsync(userId: userRole.UserId, ct: CancellationToken.None);
		afterAssign!.Remove(
			occurredAt: FakeDateProvider.Default.UtcNow,
			roleId: roleId,
			removedBy: Guid.CreateVersion7()
		);
		await SaveAsync(userRole: afterAssign);

		Core.Domains.UserRole.UserRole? restored = await _repository.GetByUserIdAsync(userId: userRole.UserId, ct: CancellationToken.None);

		await Assert.That(value: restored!.RoleIds).IsEmpty();
	}

	[Test]
	public async Task SaveAsync_WithNoEvents_ShouldNotThrowAndNotPersistAnything()
	{
		Core.Domains.UserRole.UserRole userRole = UserRoleFactory.CreateWithRole();
		userRole.ClearEvents();

		await Assert.That(action: async () => await SaveAsync(userRole: userRole)).ThrowsNothing();

		Core.Domains.UserRole.UserRole? loaded = await _repository.GetByUserIdAsync(userId: userRole.UserId, ct: CancellationToken.None);
		await Assert.That(value: loaded).IsNull();
	}

[Test]
	public async Task SaveAsync_ShouldWriteOneOutboxMessageCarryingEveryEvent()
	{
		Core.Domains.UserRole.UserRole userRole = UserRoleFactory.Create();
		userRole.Assign(
			occurredAt: FakeDateProvider.Default.UtcNow,
			roleId: Guid.CreateVersion7(),
			assignedBy: Guid.CreateVersion7()
		);
		await SaveAsync(userRole: userRole);

		List<string> payloads = await Context.OutboxMessages.AsNoTracking()
			.Where(predicate: m => m.AggregateId == userRole.UserId)
			.Select(selector: m => m.Payload)
			.ToListAsync(cancellationToken: CancellationToken.None);

		await Assert.That(value: payloads).Count().IsEqualTo(expected: 1).Because(message: """
			The event store batches one save into a single outbox row; the events travel inside it as
			envelopes, which is what lets a consumer see the whole change at once.
		""");

		using JsonDocument payload = JsonDocument.Parse(json: payloads[0]);
		JsonElement events = payload.RootElement.GetProperty(propertyName: "Events");

		await Assert.That(value: events.GetArrayLength()).IsEqualTo(expected: 2).Because(message: """
			Both UserRoleCreated and RoleAssigned must be mapped to integration events. An unmapped
			domain event is dropped with nothing but a warning, and the projection would never see it.
		""");
	}
}
