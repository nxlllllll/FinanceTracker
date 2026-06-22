using BenchmarkDotNet.Attributes;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Infrastructure.Services.Correlation;
using FinanceTracker.Infrastructure.Services.Date;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks the real <see cref="IEventStore"/> (PostgresEventStore) — snapshot lookup,
/// JSON deserialization, upcasting check — instead of a raw EF query against the events table.
/// Two load scenarios: an aggregate with 1000 events and no snapshot (full replay), and an
/// otherwise identical aggregate with a snapshot pinned at version 990 (tail replay of 10 events).
/// </summary>
public class EventStoreBenchmarks : BenchmarkBase
{
	private const int EventCount = 1000;
	private const int SnapshotAtVersion = 990;

	private Guid _noSnapshotAggregateId;
	private Guid _withSnapshotAggregateId;
	private IEventStore _eventStore = null!;

	private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
	{
		public T CurrentValue { get; } = value;
		public T Get(string? name) => CurrentValue;
		public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

		private sealed class NullDisposable : IDisposable
		{
			public static readonly NullDisposable Instance = new();
			public void Dispose() { }
		}
	}

	private static IEventStore CreateEventStore(FinanceTrackerContext context) => new PostgresEventStore(
		context: context,
		eventTypeResolver: new EventTypeResolver(assembly: typeof(IEvent).Assembly, logger: NullLogger<EventTypeResolver>.Instance),
		integrationEventMapper: new AccountIntegrationEventMapper(logger: NullLogger<AccountIntegrationEventMapper>.Instance),
		integrationEventTypeResolver: new IntegrationEventTypeResolver(
			contractsAssembly: typeof(IAccountIntegrationEvent).Assembly,
			logger: NullLogger<IntegrationEventTypeResolver>.Instance
		),
		dateProvider: new DateProvider(),
		logger: NullLogger<PostgresEventStore>.Instance,
		options: new FixedOptionsMonitor<EventStoreOptions>(new EventStoreOptions { SnapshotThreshold = 25 }),
		correlationContext: new CorrelationContext(),
		upcasterRegistry: new EventUpcasterRegistry(upcasters: [])
	);

	private static AccountDebited BuildEvent(int version) => new AccountDebited(
		Id: Guid.NewGuid(),
		AccountId: Guid.NewGuid(),
		TransactionId: Guid.NewGuid(),
		CategoryId: Guid.NewGuid(),
		Amount: 100m,
		ExchangeRate: 1m,
		Description: "bench",
		Version: version,
		OccurredAt: DateTimeOffset.UtcNow.AddSeconds(seconds: -version)
	);

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		_noSnapshotAggregateId = Guid.NewGuid();
		_withSnapshotAggregateId = Guid.NewGuid();

		await using FinanceTrackerContext context = Db.CreateContext();
		IEventStore seedEventStore = CreateEventStore(context: context);

		List<IEvent> noSnapshotEvents = Enumerable.Range(start: 1, count: EventCount).Select(selector: v => (IEvent)BuildEvent(version: v)).ToList();
		await seedEventStore.SaveAsync(
			aggregateId: _noSnapshotAggregateId,
			aggregateType: AggregateTypeNames.Account,
			events: noSnapshotEvents,
			expectedVersion: 0
		);

		List<IEvent> withSnapshotEvents = Enumerable.Range(start: 1, count: EventCount).Select(selector: v => (IEvent)BuildEvent(version: v)).ToList();
		await seedEventStore.SaveAsync(
			aggregateId: _withSnapshotAggregateId,
			aggregateType: AggregateTypeNames.Account,
			events: withSnapshotEvents,
			expectedVersion: 0
		);

		await context.SaveChangesAsync();

		await context.Snapshots.AddAsync(entity: new SnapshotEntity
		{
			AggregateId = _withSnapshotAggregateId,
			AggregateType = AggregateTypeNames.Account,
			Version = SnapshotAtVersion,
			State = "{}",
			CreatedAt = DateTimeOffset.UtcNow
		});
		await context.SaveChangesAsync();
	}

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();
		_eventStore = CreateEventStore(context: Context);
	}

	[Benchmark]
	public async Task LoadAsync_NoSnapshot_FullReplay()
		=> await _eventStore.LoadAsync(aggregateId: _noSnapshotAggregateId, aggregateType: AggregateTypeNames.Account);

	[Benchmark]
	public async Task LoadAsync_WithSnapshot_TailReplay()
		=> await _eventStore.LoadAsync(aggregateId: _withSnapshotAggregateId, aggregateType: AggregateTypeNames.Account);

	[Benchmark]
	public async Task GetAggregateIdsAsync()
	{
		await foreach (Guid _ in _eventStore.GetAggregateIdsAsync(aggregateType: AggregateTypeNames.Account))
		{ }
	}
}