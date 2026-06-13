using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Correlation;
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

	private PostgresEventStore CreateEventStore(FinanceTrackerContext? ctx = null)
	{
		IEventUpcasterRegistry upcasterRegistry = Substitute.For<IEventUpcasterRegistry>();
		upcasterRegistry.HasChain(eventType: Arg.Any<string>()).Returns(returnThis: false);

		return new PostgresEventStore(
			context: ctx ?? Context,
			eventTypeResolver: new EventTypeResolver(
				assembly: typeof(IEvent).Assembly,
				logger: Substitute.For<ILogger<EventTypeResolver>>()
			),
			integrationEventMapper: new AccountIntegrationEventMapper(
				logger: Substitute.For<ILogger<AccountIntegrationEventMapper>>()
			),
			integrationEventTypeResolver: new IntegrationEventTypeResolver(
				contractsAssembly: typeof(IAccountIntegrationEvent).Assembly,
				logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
			),
			dateProvider: FakeDateProvider.Default,
			correlationContext: Substitute.For<ICorrelationContext>(),
			upcasterRegistry: upcasterRegistry,
			options: new FakeOptionsMonitor<EventStoreOptions>(value: new EventStoreOptions()),
			logger: Substitute.For<ILogger<PostgresEventStore>>()
		);
	}

	private FinanceTrackerContext CreateFreshContext() => new FinanceTrackerContext(
		options: new DbContextOptionsBuilder<FinanceTrackerContext>().UseNpgsql(connectionString: Context.Database.GetConnectionString()!).Options
	);

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
	public async Task SaveAsync_WithConcurrentWrite_ShouldThrowConcurrencyConflictException()
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
		))).Throws<UniqueConstraintException>();
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
}