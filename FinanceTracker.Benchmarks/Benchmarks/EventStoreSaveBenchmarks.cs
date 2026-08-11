using BenchmarkDotNet.Attributes;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Infrastructure.Services.Correlation;
using FinanceTracker.Infrastructure.Services.Date;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks the real <see cref="IEventStore.SaveAsync"/> write path. Each call advances an
/// aggregate's version under optimistic concurrency, so it cannot be repeated on the same
/// aggregate within one iteration — this class runs under a dedicated config (see Program.cs)
/// with InvocationCount=1/UnrollFactor=1, so exactly one call happens per [IterationSetup],
/// which seeds a fresh aggregate every time.
/// "No snapshot" appends 1 event to a 19-event aggregate with no snapshot factory passed.
/// "With snapshot" appends 1 event to an otherwise identical aggregate, but with a threshold
/// configured so this exact append crosses a snapshot boundary, forcing one snapshot write.
/// </summary>
public class EventStoreSaveBenchmarks : BenchmarkBase
{
	private const int PreSeededEventCount = 20;

	private Guid _noSnapshotAggregateId;
	private Guid _withSnapshotAggregateId;
	private IEventStore _noSnapshotEventStore = null!;
	private IEventStore _withSnapshotEventStore = null!;

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

	private IEventStore CreateEventStore(int snapshotThreshold) => new PostgresEventStore(
		context: Context,
		eventTypeResolver: new EventTypeResolver(assembly: typeof(IEvent).Assembly, logger: NullLogger<EventTypeResolver>.Instance),
		integrationEventMapper: new AccountIntegrationEventMapper(),
		integrationEventTypeResolver: new IntegrationEventTypeResolver(
			contractsAssembly: typeof(IIntegrationEvent).Assembly,
			logger: NullLogger<IntegrationEventTypeResolver>.Instance
		),
		dateProvider: new DateProvider(),
		logger: NullLogger<PostgresEventStore>.Instance,
		options: new FixedOptionsMonitor<EventStoreOptions>(new EventStoreOptions { SnapshotThreshold = snapshotThreshold }),
		correlationContext: new CorrelationContext(),
		upcasterRegistry: new EventUpcasterRegistry(upcasters: []),
		eventSchemaHealthState: new EventSchemaHealthState(logger: NullLogger<EventSchemaHealthState>.Instance)
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

	private async Task SeedAggregateAsync(IEventStore eventStore, Guid aggregateId)
	{
		List<IEvent> events = Enumerable.Range(start: 1, count: PreSeededEventCount - 1).Select(selector: v => (IEvent)BuildEvent(version: v)).ToList();

		await eventStore.SaveAsync(
			aggregateId: aggregateId,
			aggregateType: AggregateTypeNames.Account,
			events: events,
			expectedVersion: 0
		);
		await Context.SaveChangesAsync();
	}

	[IterationSetup]
	public override void IterationSetup()
	{
		base.IterationSetup();

		_noSnapshotAggregateId = Guid.NewGuid();
		_withSnapshotAggregateId = Guid.NewGuid();

		_noSnapshotEventStore = CreateEventStore(snapshotThreshold: 1_000_000);
		_withSnapshotEventStore = CreateEventStore(snapshotThreshold: PreSeededEventCount);

		SeedAggregateAsync(eventStore: _noSnapshotEventStore, aggregateId: _noSnapshotAggregateId).GetAwaiter().GetResult();
		SeedAggregateAsync(eventStore: _withSnapshotEventStore, aggregateId: _withSnapshotAggregateId).GetAwaiter().GetResult();
	}

	[Benchmark]
	public async Task SaveAsync_NoSnapshot()
	{
		await _noSnapshotEventStore.SaveAsync(
			aggregateId: _noSnapshotAggregateId,
			aggregateType: AggregateTypeNames.Account,
			events: [BuildEvent(version: PreSeededEventCount)],
			expectedVersion: PreSeededEventCount - 1,
			snapshotFactory: null
		);
		await Context.SaveChangesAsync();
	}

	[Benchmark]
	public async Task SaveAsync_WithSnapshot()
	{
		await _withSnapshotEventStore.SaveAsync(
			aggregateId: _withSnapshotAggregateId,
			aggregateType: AggregateTypeNames.Account,
			events: [BuildEvent(version: PreSeededEventCount)],
			expectedVersion: PreSeededEventCount - 1,
			snapshotFactory: () => "{}"
		);
		await Context.SaveChangesAsync();
	}
}
