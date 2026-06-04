using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Services.Rebuild.Account;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class AccountProjectionRebuilderTests
{
	private IEventStore _eventStore = null!;
	private IAccountWriteRepository _repository = null!;
	private AccountProjectionRebuilder _rebuilder = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_eventStore = Substitute.For<IEventStore>();
		_repository = Substitute.For<IAccountWriteRepository>();

		AccountDomainEventApplier applier = new AccountDomainEventApplier(repository: _repository);

		_rebuilder = new AccountProjectionRebuilder(
			eventStore: _eventStore,
			applier: applier,
			logger: Substitute.For<ILogger<AccountProjectionRebuilder>>()
		);
	}

	[Test]
	public async Task RebuildAsync_WhenNoEvents_ShouldNotCallRepository()
	{
		Guid accountId = Guid.CreateVersion7();

		_eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: []));

		await _rebuilder.RebuildAsync(accountId: accountId, ct: CancellationToken.None);

		await _repository.DidNotReceiveWithAnyArgs().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAsync_WhenHasEvents_ShouldApplyAllEvents()
	{
		Guid accountId = Guid.CreateVersion7();

		AccountCreated created = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Карта").Value,
			Type: AccountType.Checking,
			Currency: Currency.Reconstitute(value: "RUB"),
			Balance: 1000m,
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		AccountDebited debited = new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 100m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		_eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: [created, debited]));

		await _rebuilder.RebuildAsync(accountId: accountId, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).CreateAsync(
			@event: created,
			ct: Arg.Any<CancellationToken>()
		);
		await _repository.Received(requiredNumberOfCalls: 1).DebitAsync(
			@event: debited,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAsync_WhenOnlySnapshotNoEvents_ShouldNotCallRepository()
	{
		Guid accountId = Guid.CreateVersion7();

		SnapshotData snapshot = new SnapshotData(
			AggregateId: accountId,
			AggregateType: "Account",
			Version: 10,
			State: "{}"
		);

		_eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: snapshot, Events: []));

		await _rebuilder.RebuildAsync(accountId: accountId, ct: CancellationToken.None);

		await _repository.DidNotReceiveWithAnyArgs().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAllAsync_WhenMultipleAccounts_ShouldRebuildEach()
	{
		Guid accountId1 = Guid.CreateVersion7();
		Guid accountId2 = Guid.CreateVersion7();

		_eventStore.GetAggregateIdsAsync(
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [accountId1, accountId2]);

		AccountCreated event1 = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId1,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Счёт 1").Value,
			Type: AccountType.Checking,
			Currency: Currency.Reconstitute(value: "RUB"),
			Balance: 1000m,
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		AccountCreated event2 = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId2,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Счёт 2").Value,
			Type: AccountType.Savings,
			Currency: Currency.Reconstitute(value: "USD"),
			Balance: 500m,
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		_eventStore.LoadAsync(
			aggregateId: accountId1,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: [event1]));

		_eventStore.LoadAsync(
			aggregateId: accountId2,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: [event2]));

		await _rebuilder.RebuildAllAsync(ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 2).CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAllAsync_WhenOneAccountFails_ShouldContinueWithOthers()
	{
		Guid accountId1 = Guid.CreateVersion7();
		Guid accountId2 = Guid.CreateVersion7();

		_eventStore.GetAggregateIdsAsync(
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [accountId1, accountId2]);

		_eventStore.LoadAsync(
			aggregateId: accountId1,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(ex: new InvalidOperationException(message: "EventStore unavailable."));

		AccountCreated event2 = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId2,
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Счёт 2").Value,
			Type: AccountType.Checking,
			Currency: Currency.Reconstitute(value: "RUB"),
			Balance: 100m,
			Version: 1,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		_eventStore.LoadAsync(
			aggregateId: accountId2,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: [event2]));

		await _rebuilder.RebuildAllAsync(ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAllAsync_WhenNoAccounts_ShouldNotCallRepository()
	{
		_eventStore.GetAggregateIdsAsync(
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		await _rebuilder.RebuildAllAsync(ct: CancellationToken.None);

		await _repository.DidNotReceiveWithAnyArgs().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAllAsync_WhenCancelled_ShouldStopProcessing()
	{
		Guid accountId1 = Guid.CreateVersion7();
		Guid accountId2 = Guid.CreateVersion7();

		_eventStore.GetAggregateIdsAsync(
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [accountId1, accountId2]);

		using CancellationTokenSource cts = new CancellationTokenSource();
		await cts.CancelAsync();

		await _rebuilder.RebuildAllAsync(ct: cts.Token);

		await _repository.DidNotReceiveWithAnyArgs().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}