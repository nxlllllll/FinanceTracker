using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.Rebuild;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Services.Rebuild.Account;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class AccountProjectionRebuilderTests
{
	private IEventStore _eventStore = null!;
	private IAccountWriteRepository _repository = null!;
	private ISnapshotSerializer<Account> _snapshotSerializer = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IServiceScopeFactory _scopeFactory = null!;
	private AccountProjectionRebuilder _rebuilder = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_eventStore = Substitute.For<IEventStore>();
		_repository = Substitute.For<IAccountWriteRepository>();
		_snapshotSerializer = Substitute.For<ISnapshotSerializer<Account>>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(callInfo => callInfo.Arg<Func<Task>>()());

		AccountDomainEventApplier applier = new AccountDomainEventApplier(repository: _repository);

		// ProcessBatchAsync resolves a fresh IAccountProjectionRebuilder per account from a new
		// DI scope (see production code) instead of reusing `this`. These unit tests assert
		// against the single mocked dependency graph set up above, so the fake scope factory
		// just routes straight back to this same rebuilder/dependency set rather than standing
		// up a real container — the production wiring itself is covered separately.
		_scopeFactory = Substitute.For<IServiceScopeFactory>();
		IServiceScope scope = Substitute.For<IServiceScope>();
		IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();

		scope.ServiceProvider.Returns(returnThis: serviceProvider);
		serviceProvider.GetService(serviceType: typeof(IAccountProjectionRebuilder)).Returns(returnThis: _ => _rebuilder);
		_scopeFactory.CreateScope().Returns(returnThis: scope);

		_rebuilder = new AccountProjectionRebuilder(
			eventStore: _eventStore,
			writeRepository: _repository,
			snapshotSerializer: _snapshotSerializer,
			unitOfWork: _unitOfWork,
			applier: applier,
			scopeFactory: _scopeFactory,
			logger: Substitute.For<ILogger<AccountProjectionRebuilder>>()
		);
	}

	private static AccountCreated BuildCreatedEvent(Guid accountId) => new AccountCreated(
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

	private static Account BuildAccount() 
		=> AccountFactory.Create(userId: Guid.CreateVersion7()).Value!;

	[Test]
	public async Task RebuildAsync_WhenNoEventsAndNoSnapshot_ShouldNotCallRepository()
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
		await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAsync_WhenNoSnapshot_ShouldDeleteBeforeApplyingEvents()
	{
		Guid accountId = Guid.CreateVersion7();
		AccountCreated created = BuildCreatedEvent(accountId: accountId);

		_eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: [created]));

		await _rebuilder.RebuildAsync(accountId: accountId, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			accountId: accountId,
			ct: Arg.Any<CancellationToken>()
		);
		await _repository.Received(requiredNumberOfCalls: 1).CreateAsync(
			@event: created,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAsync_WhenSnapshotExists_ShouldUpsertFromSnapshotThenApplyEvents()
	{
		Guid accountId = Guid.CreateVersion7();
		Account account = BuildAccount();

		SnapshotData snapshot = new SnapshotData(
			AggregateId: accountId,
			AggregateType: "Account",
			Version: 5,
			State: "{}"
		);

		AccountDebited debited = new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 100m,
			ExchangeRate: 1m,
			Description: null,
			Version: 6,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		_eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: snapshot, Events: [debited]));

		_snapshotSerializer.Deserialize(snapshot: snapshot).Returns(returnThis: account);

		await _rebuilder.RebuildAsync(accountId: accountId, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).UpsertFromSnapshotAsync(
			account: account,
			ct: Arg.Any<CancellationToken>()
		);
		await _repository.DidNotReceive().DeleteAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _repository.Received(requiredNumberOfCalls: 1).DebitAsync(
			@event: debited,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAsync_WhenSnapshotOnlyNoPostSnapshotEvents_ShouldUpsertAndNotApplyEvents()
	{
		Guid accountId = Guid.CreateVersion7();
		Account account = BuildAccount();

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

		_snapshotSerializer.Deserialize(snapshot: snapshot).Returns(returnThis: account);

		await _rebuilder.RebuildAsync(accountId: accountId, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).UpsertFromSnapshotAsync(
			account: account,
			ct: Arg.Any<CancellationToken>()
		);
		await _repository.DidNotReceiveWithAnyArgs().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAsync_ShouldExecuteInsideTransaction()
	{
		Guid accountId = Guid.CreateVersion7();
		AccountCreated created = BuildCreatedEvent(accountId: accountId);

		_eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: [created]));

		await _rebuilder.RebuildAsync(accountId: accountId, ct: CancellationToken.None);

		await _unitOfWork.Received(requiredNumberOfCalls: 1).ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
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
		).Returns(returnThis: _ => AsyncEnumerable(accountId1, accountId2));

		AccountCreated event1 = BuildCreatedEvent(accountId: accountId1);
		AccountCreated event2 = BuildCreatedEvent(accountId: accountId2);

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

		await _rebuilder.RebuildAllAsync(batchSize: 50, ct: CancellationToken.None);

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
		).Returns(returnThis: _ => AsyncEnumerable(accountId1, accountId2));

		_eventStore.LoadAsync(
			aggregateId: accountId1,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(ex: new InvalidOperationException(message: "EventStore unavailable."));

		AccountCreated event2 = BuildCreatedEvent(accountId: accountId2);

		_eventStore.LoadAsync(
			aggregateId: accountId2,
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: [event2]));

		await _rebuilder.RebuildAllAsync(batchSize: 50, ct: CancellationToken.None);

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
		).Returns(returnThis: _ => AsyncEnumerable<Guid>());

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
		).Returns(returnThis: _ => AsyncEnumerable(accountId1, accountId2));

		using CancellationTokenSource cts = new CancellationTokenSource();
		await cts.CancelAsync();

		await _rebuilder.RebuildAllAsync(ct: cts.Token);

		await _repository.DidNotReceiveWithAnyArgs().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAllAsync_ShouldProcessInBatches()
	{
		Guid[] ids = Enumerable.Range(start: 0, count: 7).Select(_ => Guid.CreateVersion7()).ToArray();

		_eventStore.GetAggregateIdsAsync(
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ => AsyncEnumerable(values: ids));

		foreach (Guid id in ids)
		{
			AccountCreated ev = BuildCreatedEvent(accountId: id);
			_eventStore.LoadAsync(
				aggregateId: id,
				aggregateType: Arg.Any<string>(),
				ct: Arg.Any<CancellationToken>()
			).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: [ev]));
		}

		await _rebuilder.RebuildAllAsync(batchSize: 3, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 7).CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task RebuildAllAsync_ShouldCreateANewDiScopePerAccount()
	{
		Guid accountId1 = Guid.CreateVersion7();
		Guid accountId2 = Guid.CreateVersion7();

		_eventStore.GetAggregateIdsAsync(
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ => AsyncEnumerable(accountId1, accountId2));

		_eventStore.LoadAsync(
			aggregateId: Arg.Any<Guid>(),
			aggregateType: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new EventStoreResult(Snapshot: null, Events: []));

		await _rebuilder.RebuildAllAsync(batchSize: 50, ct: CancellationToken.None);

		_scopeFactory.Received(requiredNumberOfCalls: 2).CreateScope();
	}

	private static async IAsyncEnumerable<T> AsyncEnumerable<T>(params T[] values)
	{
		foreach (T value in values)
		{
			await Task.Yield();
			yield return value;
		}
	}
}