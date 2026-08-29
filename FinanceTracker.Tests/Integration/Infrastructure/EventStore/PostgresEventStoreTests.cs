using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.EventStore;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.EventStore;

public sealed class PostgresEventStoreTests : DatabaseFixture
{
	private PostgresEventStore _eventStore = null!;
	private EFUnitOfWork _unitOfWork = null!;
	private readonly AccountSnapshotSerializer _serializer = new AccountSnapshotSerializer();

	private PostgresEventStore CreateEventStore(
		FinanceTrackerContext? ctx = null,
		EventStoreOptions? eventStoreOptions = null,
		IEventUpcasterRegistry? upcasterRegistry = null,
		IEventSchemaHealthState? eventSchemaHealthState = null)
	{
		IEventUpcasterRegistry registry = upcasterRegistry ?? Substitute.For<IEventUpcasterRegistry>();
		if (upcasterRegistry is null)
			registry.HasChain(eventType: Arg.Any<string>()).Returns(returnThis: false);

		return new PostgresEventStore(
			context: ctx ?? Context,
			eventTypeResolver: new EventTypeResolver(
				assembly: typeof(IEvent).Assembly,
				logger: Substitute.For<ILogger<EventTypeResolver>>()
			),
			integrationEventMapper: new AccountIntegrationEventMapper(),
			integrationEventTypeResolver: new IntegrationEventTypeResolver(
				contractsAssembly: typeof(IIntegrationEvent).Assembly,
				logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
			),
			dateProvider: FakeDateProvider.Default,
			correlationContext: Substitute.For<ICorrelationContext>(),
			upcasterRegistry: registry,
			options: new FakeOptionsMonitor<EventStoreOptions>(value: eventStoreOptions ?? new EventStoreOptions()),
			logger: Substitute.For<ILogger<PostgresEventStore>>(),
			eventSchemaHealthState: eventSchemaHealthState ?? Substitute.For<IEventSchemaHealthState>()
		);
	}

	private FinanceTrackerContext CreateFreshContext() => new FinanceTrackerContext(
		options: new DbContextOptionsBuilder<FinanceTrackerContext>().UseNpgsql(connectionString: Context.Database.GetConnectionString()!).Options
	);

	private static AccountCreated CreatedFor(Guid accountId) => new AccountCreated(
		Id: Guid.CreateVersion7(),
		AccountId: accountId,
		UserId: Guid.CreateVersion7(),
		Name: Name.Create(value: "Новый счёт").Value,
		Type: AccountType.Checking,
		Currency: Currency.Create(value: "RUB").Value,
		Balance: 0,
		Version: 0,
		OccurredAt: DateTimeOffset.UtcNow
	);

	private Task StoreSchemaVersionAsync(Guid accountId, int schemaVersion)
	{
		return Context.Events.Where(predicate: e => e.AggregateId == accountId).ExecuteUpdateAsync(
			setPropertyCalls: setters => setters.SetProperty(e => e.SchemaVersion, schemaVersion)
		);
	}

	private async Task<Guid> SaveOneCreatedEventAsync()
	{
		Guid accountId = Guid.CreateVersion7();

		await SaveAsync(
			store: _eventStore,
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [CreatedFor(accountId: accountId)],
			expectedVersion: 0
		);

		return accountId;
	}

	private async Task<Account> SaveAccountWithSnapshotAsync()
	{
		Account account = AccountFactory.Create(balance: 0m, currency: "RUB").Value!;

		await SaveAsync(
			store: _eventStore,
			aggregateId: account.Id,
			aggregateType: AggregateTypeNames.Account,
			events: account.Events,
			expectedVersion: 0,
			snapshotFactory: () => _serializer.Serialize(aggregate: account)
		);
		account.ClearEvents();

		for (int i = 0; i < 30; i++)
		{
			account.Credit(
				occurredAt: FakeDateProvider.Default.UtcNow,
				transactionId: Guid.CreateVersion7(),
				categoryId: Guid.CreateVersion7(),
				amount: 10m,
				exchangeRate: 1m,
				description: null
			);

			await SaveAsync(
				store: _eventStore,
				aggregateId: account.Id,
				aggregateType: AggregateTypeNames.Account,
				events: account.Events,
				expectedVersion: account.Version - account.Events.Count,
				snapshotFactory: () => _serializer.Serialize(aggregate: account)
			);
			account.ClearEvents();
		}

		return account;
	}

	[Before(hookType: Test)]
	public void SetupEventStore()
	{
		_unitOfWork = new EFUnitOfWork(context: Context, logger: Substitute.For<ILogger<EFUnitOfWork>>());
		_eventStore = CreateEventStore();
	}

	[After(hookType: Test)]
	public async Task TearDownAsync()
		=> await _unitOfWork.DisposeAsync();

	private Task SaveAsync(
		PostgresEventStore store,
		Guid aggregateId,
		string aggregateType,
		IEnumerable<IEvent> events,
		int expectedVersion,
		Func<string>? snapshotFactory = null)
	{
		return _unitOfWork.ExecuteInTransactionAsync(operation: async () => await store.SaveAsync(
			aggregateId: aggregateId,
			aggregateType: aggregateType,
			events: events,
			expectedVersion: expectedVersion,
			snapshotFactory: snapshotFactory
		));
	}

	[Test]
	public async Task SaveAsync_WithNewEvents_ShouldPersistToDatabase()
	{
		Guid accountId = Guid.CreateVersion7();
		AccountCreated @event = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Новый счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			Balance: 0,
			Version: 0,
			OccurredAt: DateTimeOffset.UtcNow
		);

		await SaveAsync(
			store: _eventStore,
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [@event],
			expectedVersion: 0
		);

		EventStoreResult result = await _eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account
		);
		await Assert.That(value: result.Events.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task LoadAsync_WithSavedEvents_ShouldReturnEventsInOrder()
	{
		Guid accountId = Guid.CreateVersion7();
		AccountCreated created = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Новый счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			Balance: 1000m,
			Version: 0,
			OccurredAt: DateTimeOffset.UtcNow
		);
		AccountDebited debited = new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			Version: 0,
			OccurredAt: DateTimeOffset.UtcNow
		);

		await SaveAsync(
			store: _eventStore,
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [created, debited],
			expectedVersion: 0
		);

		EventStoreResult result = await _eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account
		);

		await Assert.That(value: result.Events.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.Events[0]).IsTypeOf<AccountCreated>();
		await Assert.That(value: result.Events[1]).IsTypeOf<AccountDebited>();
	}

	[Test]
	public async Task LoadAsync_WithNonExistentAggregate_ShouldReturnEmptyList()
	{
		EventStoreResult result = await _eventStore.LoadAsync(
			aggregateId: Guid.CreateVersion7(),
			aggregateType: AggregateTypeNames.Account
		);
		await Assert.That(value: result.Events.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task LoadAsync_WhenTheStoredSchemaIsAheadOfTheBuild_ShouldRefuseAndMarkTheSchemaIncompatible()
	{
		Guid accountId = await SaveOneCreatedEventAsync();
		await StoreSchemaVersionAsync(accountId: accountId, schemaVersion: 99);

		IEventSchemaHealthState healthState = Substitute.For<IEventSchemaHealthState>();
		PostgresEventStore store = CreateEventStore(eventSchemaHealthState: healthState);

		await Assert.That(action: async () => await store.LoadAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account
		)).Throws<IncompatibleEventVersionException>();

		healthState.Received(requiredNumberOfCalls: 1).MarkIncompatible(diagnosis: Arg.Any<string>());
	}

	[Test]
	public async Task LoadAsync_WhenAnOlderSchemaHasNoUpcaster_ShouldRefuseAndMarkTheSchemaIncompatible()
	{
		Guid accountId = await SaveOneCreatedEventAsync();
		await StoreSchemaVersionAsync(accountId: accountId, schemaVersion: 0);

		IEventSchemaHealthState healthState = Substitute.For<IEventSchemaHealthState>();
		PostgresEventStore store = CreateEventStore(eventSchemaHealthState: healthState);

		await Assert.That(action: async () => await store.LoadAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account
		)).Throws<IncompatibleEventVersionException>()
			.Because(message: "defaulting the fields the new shape added would rebuild the aggregate into a state it never held");

		healthState.Received(requiredNumberOfCalls: 1).MarkIncompatible(diagnosis: Arg.Any<string>());
	}

	[Test]
	public async Task LoadAsync_WhenAnOlderSchemaHasAnUpcaster_ShouldMigrateInsteadOfRefusing()
	{
		Guid accountId = await SaveOneCreatedEventAsync();
		await StoreSchemaVersionAsync(accountId: accountId, schemaVersion: 0);

		AccountCreated migrated = CreatedFor(accountId: accountId);

		IEventUpcasterRegistry registry = Substitute.For<IEventUpcasterRegistry>();
		registry.HasChain(eventType: Arg.Any<string>()).Returns(returnThis: true);
		registry.Apply(
			eventType: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			storedVersion: Arg.Is<int>(predicate: version => version == 0),
			currentVersion: Arg.Any<int>()
		).Returns(returnThis: migrated);

		IEventSchemaHealthState healthState = Substitute.For<IEventSchemaHealthState>();
		PostgresEventStore store = CreateEventStore(upcasterRegistry: registry, eventSchemaHealthState: healthState);

		EventStoreResult result = await store.LoadAsync(aggregateId: accountId, aggregateType: AggregateTypeNames.Account);

		await Assert.That(value: result.Events.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Events[0]).IsSameReferenceAs(expected: migrated);

		healthState.DidNotReceive().MarkIncompatible(diagnosis: Arg.Any<string>());
	}


	[Test]
	public async Task LoadAsync_WithSnapshot_ShouldRestoreCorrectState()
	{
		Result<Account, DomainException> o = Account.Create(
			occurredAt: FakeDateProvider.Default.UtcNow,
			userId: Guid.CreateVersion7(),
			name: Name.Create(value: "Счёт накопительный").Value,
			type: AccountType.Savings,
			currency: Currency.Create(value: "USD").Value,
			balance: 1000m
		);

		Account original = o.Value!;

		await SaveAsync(
			store: _eventStore,
			aggregateId: original.Id,
			aggregateType: AggregateTypeNames.Account,
			events: original.Events,
			expectedVersion: 0,
			snapshotFactory: () => _serializer.Serialize(aggregate: original)
		);
		original.ClearEvents();

		for (int i = 0; i < 49; i++)
		{
			original.Credit(
				occurredAt: FakeDateProvider.Default.UtcNow,
				transactionId: Guid.CreateVersion7(),
				categoryId: Guid.CreateVersion7(),
				amount: 10m,
				exchangeRate: 1m,
				description: null
			);
			int expectedVersion = original.Version - original.Events.Count;
			await SaveAsync(
				store: _eventStore,
				aggregateId: original.Id,
				aggregateType: AggregateTypeNames.Account,
				events: original.Events,
				expectedVersion: expectedVersion,
				snapshotFactory: () => _serializer.Serialize(aggregate: original)
			);
			original.ClearEvents();
		}

		EventStoreResult result = await _eventStore.LoadAsync(
			aggregateId: original.Id,
			aggregateType: AggregateTypeNames.Account
		);

		Account restored = Account.Reconstitute(
			snapshot: result.Snapshot,
			events: result.Events,
			serializer: _serializer
		);

		await Assert.That(value: restored.Id).IsEqualTo(expected: original.Id);
		await Assert.That(value: restored.Name).IsEqualTo(expected: original.Name);
		await Assert.That(value: restored.Balance.Amount).IsEqualTo(expected: 1490m);
		await Assert.That(value: restored.Version).IsEqualTo(expected: 50);
	}

	[Test]
	public async Task SaveAsync_WithStaleExpectedVersion_ShouldThrowConcurrencyConflictException()
	{
		Guid accountId = Guid.CreateVersion7();
		AccountCreated @event = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Новый счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			Balance: 0,
			Version: 0,
			OccurredAt: DateTimeOffset.UtcNow
		);

		await SaveAsync(
			store: _eventStore,
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [@event],
			expectedVersion: 0
		);

		FinanceTrackerContext secondContext = CreateFreshContext();
		await using EFUnitOfWork secondUoW = new EFUnitOfWork(
			context: secondContext,
			logger: Substitute.For<ILogger<EFUnitOfWork>>()
		);
		PostgresEventStore secondStore = CreateEventStore(ctx: secondContext);

		await Assert.That(async () => await secondUoW.ExecuteInTransactionAsync(operation: async () => await secondStore.SaveAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [new AccountDebited(
				Id: Guid.CreateVersion7(),
				AccountId: accountId,
				TransactionId: Guid.CreateVersion7(),
				CategoryId: Guid.CreateVersion7(),
				Amount: 100m,
				ExchangeRate: 1m,
				Description: null,
				Version: 0,
				OccurredAt: DateTimeOffset.UtcNow
			)],
			expectedVersion: 0
		))).Throws<ConcurrencyConflictException>();
	}

	[Test]
	public async Task SaveAsync_WithPreValidateExpectedVersionEnabled_AndStaleVersion_ShouldThrowWithoutRequiringCommit()
	{
		Guid accountId = Guid.CreateVersion7();
		AccountCreated @event = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Новый счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			Balance: 0,
			Version: 0,
			OccurredAt: DateTimeOffset.UtcNow
		);

		await SaveAsync(
			store: _eventStore,
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [@event],
			expectedVersion: 0
		);

		PostgresEventStore preValidatingStore = CreateEventStore(eventStoreOptions: new EventStoreOptions { PreValidateExpectedVersion = true });

		await Assert.That(async () => await preValidatingStore.SaveAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [new AccountDebited(
				Id: Guid.CreateVersion7(),
				AccountId: accountId,
				TransactionId: Guid.CreateVersion7(),
				CategoryId: Guid.CreateVersion7(),
				Amount: 100m,
				ExchangeRate: 1m,
				Description: null,
				Version: 0,
				OccurredAt: DateTimeOffset.UtcNow
			)],
			expectedVersion: 0
		)).Throws<ConcurrencyConflictException>();
	}

	[Test]
	public async Task SaveAsync_WithTrueConcurrentWrite_ShouldThrowConcurrencyConflictException()
	{
		Guid accountId = Guid.CreateVersion7();

		AccountCreated EventFor(Guid eventId) => new AccountCreated(
			Id: eventId,
			AccountId: accountId,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Новый счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			Balance: 0,
			Version: 0,
			OccurredAt: DateTimeOffset.UtcNow
		);

		FinanceTrackerContext contextA = CreateFreshContext();
		FinanceTrackerContext contextB = CreateFreshContext();

		await using EFUnitOfWork uowA = new EFUnitOfWork(context: contextA, logger: Substitute.For<ILogger<EFUnitOfWork>>());
		await using EFUnitOfWork uowB = new EFUnitOfWork(context: contextB, logger: Substitute.For<ILogger<EFUnitOfWork>>());

		PostgresEventStore storeA = CreateEventStore(ctx: contextA);
		PostgresEventStore storeB = CreateEventStore(ctx: contextB);

		await uowA.BeginTransactionAsync();
		await uowB.BeginTransactionAsync();

		await storeA.SaveAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [EventFor(eventId: Guid.CreateVersion7())],
			expectedVersion: 0
		);
		await storeB.SaveAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [EventFor(eventId: Guid.CreateVersion7())],
			expectedVersion: 0
		);

		await uowA.CommitAsync();

		await Assert.That(async () => await uowB.CommitAsync()).Throws<ConcurrencyConflictException>();
	}

	[Test]
	public async Task SaveAsync_WhenVersionReaches50_ShouldCreateSnapshot()
	{
		Result<Account, DomainException> a = Account.Create(
			occurredAt: FakeDateProvider.Default.UtcNow,
			userId: Guid.CreateVersion7(),
			name: Name.Create(value: "Счёт").Value,
			type: AccountType.Checking,
			currency: Currency.Create(value: "RUB").Value,
			balance: 100m
		);

		Account account = a.Value!;

		await SaveAsync(
			store: _eventStore,
			aggregateId: account.Id,
			aggregateType: AggregateTypeNames.Account,
			events: account.Events,
			expectedVersion: 0,
			snapshotFactory: () => _serializer.Serialize(aggregate: account)
		);
		account.ClearEvents();

		for (int i = 0; i < 49; i++)
		{
			account.Debit(
				occurredAt: FakeDateProvider.Default.UtcNow,
				transactionId: Guid.CreateVersion7(),
				categoryId: Guid.CreateVersion7(),
				amount: 1m,
				exchangeRate: 1m,
				description: null
			);
			int expectedVersion = account.Version - account.Events.Count;
			await SaveAsync(
				store: _eventStore,
				aggregateId: account.Id,
				aggregateType: AggregateTypeNames.Account,
				events: account.Events,
				expectedVersion: expectedVersion,
				snapshotFactory: () => _serializer.Serialize(aggregate: account)
			);
			account.ClearEvents();
		}

		EventStoreResult result = await _eventStore.LoadAsync(
			aggregateId: account.Id,
			aggregateType: AggregateTypeNames.Account
		);

		await Assert.That(value: result.Snapshot).IsNotNull();
		await Assert.That(value: result.Snapshot!.Version).IsEqualTo(expected: 50);
		await Assert.That(value: result.Events.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task SaveAsync_WithNoEvents_ShouldWriteNothingAtAll()
	{
		Guid accountId = Guid.CreateVersion7();

		await SaveAsync(
			store: _eventStore,
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [],
			expectedVersion: 0
		);

		await Assert.That(value: await Context.Events.CountAsync(predicate: e => e.AggregateId == accountId)).IsEqualTo(expected: 0);
		await Assert.That(value: await Context.OutboxMessages.CountAsync(predicate: m => m.AggregateId == accountId)).IsEqualTo(expected: 0)
			.Because(message: "an empty save is a no-op, and an outbox row for it would publish a change that never happened");
	}

	[Test]
	public async Task SaveAsync_ShouldRaiseOneOutboxMessageForTheWholeBatch()
	{
		Guid accountId = Guid.CreateVersion7();

		await SaveAsync(
			store: _eventStore,
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			events: [CreatedFor(accountId: accountId), CreatedFor(accountId: accountId)],
			expectedVersion: 0
		);

		await Assert.That(value: await Context.OutboxMessages.CountAsync(predicate: m => m.AggregateId == accountId)).IsEqualTo(expected: 1)
			.Because(message: "the outbox carries one envelope per save, which is what keeps ordering per aggregate meaningful");
	}

	[Test]
	public async Task GetAggregateIdsAsync_ShouldListEachAggregateOnceAndOnlyOfTheRequestedType()
	{
		Guid first = await SaveOneCreatedEventAsync();
		Guid second = await SaveOneCreatedEventAsync();

		await SaveAsync(
			store: _eventStore,
			aggregateId: first,
			aggregateType: AggregateTypeNames.Account,
			events: [CreatedFor(accountId: first)],
			expectedVersion: 1
		);

		List<Guid> ids = [];
		await foreach (Guid id in _eventStore.GetAggregateIdsAsync(aggregateType: AggregateTypeNames.Account))
			ids.Add(item: id);

		await Assert.That(value: ids).Contains(expected: first);
		await Assert.That(value: ids).Contains(expected: second);
		await Assert.That(value: ids.Count(predicate: id => id == first)).IsEqualTo(expected: 1)
			.Because(message: "a rebuild iterates this list, and a repeated id would replay the same aggregate twice");
	}

	[Test]
	public async Task LoadAllEventsAsync_ShouldReturnTheWholeHistory()
	{
		Account account = await SaveAccountWithSnapshotAsync();

		IReadOnlyList<IEvent> events = await _eventStore.LoadAllEventsAsync(
			aggregateId: account.Id,
			aggregateType: AggregateTypeNames.Account
		);

		await Assert.That(value: events.Count).IsEqualTo(expected: 31).Because(message: """
			One creation and thirty credits. The count is the point: LoadAsync would return only what came
			after the snapshot, and a rebuild starting there would replay a fraction of the history onto an
			empty projection.
		""");
		await Assert.That(value: events[0]).IsTypeOf<AccountCreated>();
	}

	[Test]
	public async Task LoadAllEventsAsync_ShouldReturnMoreThanLoadAsyncWhenASnapshotExists()
	{
		Account account = await SaveAccountWithSnapshotAsync();

		EventStoreResult withSnapshot = await _eventStore.LoadAsync(
			aggregateId: account.Id,
			aggregateType: AggregateTypeNames.Account
		);

		IReadOnlyList<IEvent> everything = await _eventStore.LoadAllEventsAsync(
			aggregateId: account.Id,
			aggregateType: AggregateTypeNames.Account
		);

		await Assert.That(value: withSnapshot.Snapshot).IsNotNull().Because(message: """
			Without a snapshot the two loads agree, and the comparison below would pass while proving nothing.
		""");
		await Assert.That(value: everything.Count).IsGreaterThan(minimum: withSnapshot.Events.Count).Because(message: """
			This is the whole reason the method exists. A snapshot is derived data, and a rebuild is what you
			reach for when derived data is suspect — reading through one would carry the corruption being
			repaired straight back in.
		""");
	}

	[Test]
	public async Task LoadAllEventsAsync_ShouldReturnEventsInVersionOrder()
	{
		Account account = await SaveAccountWithSnapshotAsync();

		IReadOnlyList<IEvent> events = await _eventStore.LoadAllEventsAsync(
			aggregateId: account.Id,
			aggregateType: AggregateTypeNames.Account
		);

		IReadOnlyList<int> versions = events.Select(selector: e => e.Version).ToList();

		await Assert.That(value: versions).IsEquivalentTo(expected: versions.Order().ToList()).Because(message: """
			A projection applies deltas in the order they happened. Out of order, an archive arriving before
			the rename it followed leaves the read model holding a state the aggregate was never in.
		""");
	}

	[Test]
	public async Task LoadAllEventsAsync_ForAnUnknownAggregate_ShouldReturnEmpty()
	{
		IReadOnlyList<IEvent> events = await _eventStore.LoadAllEventsAsync(
			aggregateId: Guid.CreateVersion7(),
			aggregateType: AggregateTypeNames.Account
		);

		await Assert.That(value: events).IsEmpty().Because(message: """
			The rebuilder distinguishes "no history" from "history I could not read" and leaves the projection
			untouched in the first case — a mistyped id must not become data loss.
		""");
	}

	[Test]
	public async Task LoadAllEventsAsync_ShouldIgnoreOtherAggregatesOfTheSameType()
	{
		Guid firstId = await SaveOneCreatedEventAsync();
		await SaveOneCreatedEventAsync();

		IReadOnlyList<IEvent> events = await _eventStore.LoadAllEventsAsync(
			aggregateId: firstId,
			aggregateType: AggregateTypeNames.Account
		);

		await Assert.That(value: events.Count).IsEqualTo(expected: 1);
	}
}
