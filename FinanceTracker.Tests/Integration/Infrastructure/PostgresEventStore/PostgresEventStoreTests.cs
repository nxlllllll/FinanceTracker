using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.PostgresEventStore;

public sealed class PostgresEventStoreTests : DatabaseFixture
{
    private FinanceTracker.Infrastructure.Database.EventStore.PostgresEventStore _eventStore = null!;

    private FinanceTracker.Infrastructure.Database.EventStore.PostgresEventStore CreateEventStore()
    {
        return new FinanceTracker.Infrastructure.Database.EventStore.PostgresEventStore(
            context: new FinanceTrackerContext(new DbContextOptionsBuilder<FinanceTrackerContext>()
                .UseNpgsql(connectionString: Context.Database.GetConnectionString()!)
                .Options
            ),
            eventTypeResolver: new EventTypeResolver(assembly: typeof(IEvent).Assembly),
            dateProvider: FakeDateProvider.Default
        );
    }
    
    [Before(hookType: Test)]
    public void SetupEventStore()
        => _eventStore = CreateEventStore();

    [Test]
    public async Task SaveAsync_WithNewEvents_ShouldPersistToDatabase()
    {
        Guid accountId = Guid.NewGuid();
        AccountCreated @event = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: accountId,
            UserId: Guid.NewGuid(),
            Name: "Карта Сбер",
            Type: Core.Domains.Account.AccountType.Checking,
            Currency: "RUB",
            Balance: 0,
            OccurredAt: DateTime.UtcNow
        );

        await _eventStore.SaveAsync(
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
        Guid accountId = Guid.NewGuid();
        AccountCreated created = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: accountId,
            UserId: Guid.NewGuid(),
            Name: "Карта Сбер",
            Type: Core.Domains.Account.AccountType.Checking,
            Currency: "RUB",
            Balance: 1000m,
            OccurredAt: DateTime.UtcNow
        );
        AccountDebited debited = new AccountDebited(
            Id: Guid.NewGuid(),
            AccountId: accountId,
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 500m,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        await _eventStore.SaveAsync(
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
            aggregateId: Guid.NewGuid(),
            aggregateType: AggregateTypeNames.Account
        );
        await Assert.That(value: result.Events.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task SaveAsync_WithConcurrentWrite_ShouldThrowConcurrencyConflictException()
    {
        Guid accountId = Guid.NewGuid();
        AccountCreated @event = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: accountId,
            UserId: Guid.NewGuid(),
            Name: "Карта Сбер",
            Type: Core.Domains.Account.AccountType.Checking,
            Currency: "RUB",
            Balance: 0,
            OccurredAt: DateTime.UtcNow
        );

        FinanceTracker.Infrastructure.Database.EventStore.PostgresEventStore firstStore = CreateEventStore();
        FinanceTracker.Infrastructure.Database.EventStore.PostgresEventStore secondStore = CreateEventStore();

        await firstStore.SaveAsync(
            aggregateId: accountId,
            aggregateType: AggregateTypeNames.Account,
            events: [@event],
            expectedVersion: 0
        );

        await Assert.That(async () =>
        {
            await secondStore.SaveAsync(
                aggregateId: accountId,
                aggregateType: AggregateTypeNames.Account,
                events: [new AccountDebited(
                    Id: Guid.NewGuid(),
                    AccountId: accountId,
                    TransactionId: Guid.NewGuid(),
                    CategoryId: Guid.NewGuid(),
                    Amount: 100m,
                    ExchangeRate: 1m,
                    Description: null,
                    OccurredAt: DateTime.UtcNow
                )],
                expectedVersion: 0
            );
        }).Throws<ConcurrencyConflictException>();
    }
    
    [Test]
    public async Task SaveAsync_WhenVersionReaches50_ShouldCreateSnapshot()
    {
        Core.Domains.Account.Account account = Core.Domains.Account.Account.Create(
            occurredAt: FakeDateProvider.Default.UtcNow,
            userId: Guid.NewGuid(),
            name: "Тест",
            type: Core.Domains.Account.AccountType.Checking,
            currency: "RUB",
            balance: 100m
        );

        await _eventStore.SaveAsync(
            aggregateId: account.Id,
            aggregateType: AggregateTypeNames.Account,
            events: account.Events,
            expectedVersion: 0,
            snapshotFactory: account.TakeSnapshot
        );
        account.ClearEvents();

        for (int i = 0; i < 49; i++)
        {
            account.Debit(
                occurredAt: FakeDateProvider.Default.UtcNow,
                transactionId: Guid.NewGuid(),
                categoryId: Guid.NewGuid(),
                amount: 1m,
                exchangeRate: 1m,
                description: null
            );
            int expectedVersion = account.Version - account.Events.Count;
            await _eventStore.SaveAsync(
                aggregateId: account.Id,
                aggregateType: AggregateTypeNames.Account,
                events: account.Events,
                expectedVersion: expectedVersion,
                snapshotFactory: account.TakeSnapshot
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
        Core.Domains.Account.Account original = Core.Domains.Account.Account.Create(
            occurredAt: FakeDateProvider.Default.UtcNow,
            userId: Guid.NewGuid(),
            name: "Тест снапшота",
            type: Core.Domains.Account.AccountType.Savings,
            currency: "USD",
            balance: 1000m
        );

        await _eventStore.SaveAsync(
            aggregateId: original.Id,
            aggregateType: AggregateTypeNames.Account,
            events: original.Events,
            expectedVersion: 0,
            snapshotFactory: original.TakeSnapshot
        );
        original.ClearEvents();

        for (int i = 0; i < 49; i++)
        {
            original.Credit(
                occurredAt: FakeDateProvider.Default.UtcNow,
                transactionId: Guid.NewGuid(),
                categoryId: Guid.NewGuid(),
                amount: 10m,
                exchangeRate: 1m,
                description: null
            );
            int expectedVersion = original.Version - original.Events.Count;
            await _eventStore.SaveAsync(
                aggregateId: original.Id,
                aggregateType: AggregateTypeNames.Account,
                events: original.Events,
                expectedVersion: expectedVersion,
                snapshotFactory: original.TakeSnapshot
            );
            original.ClearEvents();
        }

        EventStoreResult result = await _eventStore.LoadAsync(
            aggregateId: original.Id,
            aggregateType: AggregateTypeNames.Account
        );
        Core.Domains.Account.Account restored = Core.Domains.Account.Account.Restore(snapshot: result.Snapshot!);
        restored.LoadEventsFromHistory(history: result.Events);

        await Assert.That(value: restored.Id).IsEqualTo(expected: original.Id);
        await Assert.That(value: restored.Name).IsEqualTo(expected: original.Name);
        await Assert.That(value: restored.Balance.Amount).IsEqualTo(expected: 1490m);
        await Assert.That(value: restored.Version).IsEqualTo(expected: 50);
    }
}