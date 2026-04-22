using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Infrastructure.Database.EventStore;

namespace FinanceTracker.Tests.Integration.Infrastructure;

public sealed class PostgresEventStoreTests : DatabaseFixture
{
    private PostgresEventStore _eventStore = null!;

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
            AccountType: "checking",
            Currency: "RUB",
            Balance: 0,
            OccurredAt: DateTime.UtcNow
        );

        await _eventStore.SaveAsync(
            aggregateId: accountId,
            aggregateType: nameof(Account),
            events: [@event],
            expectedVersion: 0
        );

        IReadOnlyList<IEvent> loaded = await _eventStore.LoadAsync(aggregateId: accountId);
        await Assert.That(value: loaded.Count).IsEqualTo(expected: 1);
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
            AccountType: "checking",
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
            aggregateType: nameof(Account),
            events: [created, debited],
            expectedVersion: 0
        );

        IReadOnlyList<IEvent> loaded = await _eventStore.LoadAsync(aggregateId: accountId);

        await Assert.That(value: loaded.Count).IsEqualTo(expected: 2);
        await Assert.That(value: loaded[0]).IsTypeOf<AccountCreated>();
        await Assert.That(value: loaded[1]).IsTypeOf<AccountDebited>();
    }

    [Test]
    public async Task LoadAsync_WithNonExistentAggregate_ShouldReturnEmptyList()
    {
        IReadOnlyList<IEvent> loaded = await _eventStore.LoadAsync(aggregateId: Guid.NewGuid());
        await Assert.That(value: loaded.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task SaveAsync_WithConcurrentWrite_ShouldThrowInvalidOperationException()
    {
        Guid accountId = Guid.NewGuid();
        AccountCreated @event = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: accountId,
            UserId: Guid.NewGuid(),
            Name: "Карта Сбер",
            AccountType: "checking",
            Currency: "RUB",
            Balance: 0,
            OccurredAt: DateTime.UtcNow
        );

        PostgresEventStore firstStore = CreateEventStore();
        PostgresEventStore secondStore = CreateEventStore();

        await firstStore.SaveAsync(
            aggregateId: accountId,
            aggregateType: nameof(Account),
            events: [@event],
            expectedVersion: 0
        );

        await Assert.That(async () =>
        {
            await secondStore.SaveAsync(
                aggregateId: accountId,
                aggregateType: nameof(Account),
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
        }).Throws<InvalidOperationException>();
    }
}