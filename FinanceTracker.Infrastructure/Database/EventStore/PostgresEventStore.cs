using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Tracing;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.EventStore;
using FinanceTracker.Infrastructure.Database.Context.Outbox;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class PostgresEventStore(
	FinanceTrackerContext context,
	IEventTypeResolver eventTypeResolver,
	IIntegrationEventMapper integrationEventMapper,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	IDateProvider dateProvider,
	ILogger<PostgresEventStore> logger,
	IOptionsMonitor<EventStoreOptions> options,
	ICorrelationContext correlationContext,
	IEventUpcasterRegistry upcasterRegistry
) : IEventStore
{
	private (List<EventEntity> Entities, List<OutboxEventEnvelope> Envelopes) BuildEntities(
		Guid aggregateId,
		string aggregateType,
		List<IEvent> eventList,
		int expectedVersion,
		DateTimeOffset now)
	{
		int currentVersion = expectedVersion;
		List<EventEntity> entities = new List<EventEntity>(capacity: eventList.Count);
		List<OutboxEventEnvelope> envelopes = new List<OutboxEventEnvelope>(capacity: eventList.Count);

		foreach (IEvent @event in eventList)
		{
			string serialized = JsonSerializer.Serialize(value: @event, inputType: @event.GetType(), options: FinanceTrackerJsonOptions.Payload);
			string? eventType = @event.GetType().GetCustomAttribute<EventTypeAttribute>()?.Name;
			if (eventType is null)
			{
				logger.ZLogError(message: $"Configuration error: {@event.GetType().Name} is missing [EventType] attribute.");
				throw new UnknownEventTypeException(message: "The following IEvent classes are missing [EventType] attribute.", eventTypes: [@event.GetType().Name]);
			}

			entities.Add(item: new EventEntity()
			{
				Id = @event.Id,
				AggregateId = aggregateId,
				AggregateType = aggregateType,
				EventType = eventType,
				SchemaVersion = eventTypeResolver.GetCurrentVersion(typeName: eventType),
				CorrelationId = correlationContext.CorrelationId,
				Version = ++currentVersion,
				Payload = serialized,
				OccurredAt = @event.OccurredAt,
				CreatedAt = now
			});

			IIntegrationEvent? integrationEvent = integrationEventMapper.Map(@event: @event);
			if (integrationEvent is null)
				continue;

			string outboxEventType = integrationEventTypeResolver.ResolveTypeName(eventType: integrationEvent.GetType());
			string outboxPayload = JsonSerializer.Serialize(value: integrationEvent, inputType: integrationEvent.GetType(), options: FinanceTrackerJsonOptions.Payload);
			envelopes.Add(item: new OutboxEventEnvelope(EventType: outboxEventType, EventPayload: outboxPayload));
		}

		return (entities, envelopes);
	}

	private async Task ApplySnapshot(
		Guid aggregateId,
		string aggregateType,
		int expectedVersion,
		Func<string>? snapshotFactory,
		int eventsCount,
		CancellationToken ct = default)
	{
		int newVersion = expectedVersion + eventsCount;
		int previousThreshold = expectedVersion / options.CurrentValue.SnapshotThreshold;
		int newThreshold = newVersion / options.CurrentValue.SnapshotThreshold;

		if (snapshotFactory is null || newThreshold <= previousThreshold)
			return;

		string snapshot = snapshotFactory();

		await context.Snapshots.AddAsync(entity: new SnapshotEntity()
		{
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			Version = newVersion,
			State = snapshot,
			CreatedAt = dateProvider.UtcNow
		}, cancellationToken: ct);
	}

	private async Task EnsureExpectedVersionAsync(
		Guid aggregateId,
		string aggregateType,
		int expectedVersion,
		CancellationToken ct)
	{
		int? currentVersion = await context.Events.AsNoTracking().Where(predicate: e => e.AggregateId == aggregateId && e.AggregateType == aggregateType)
			.MaxAsync(selector: e => (int?)e.Version, cancellationToken: ct);

		if ((currentVersion ?? 0) != expectedVersion)
		{
			logger.ZLogWarning(message: $"Concurrency conflict on aggregate {aggregateId} ({aggregateType}): expected version {expectedVersion}, actual {currentVersion ?? 0}.");
			throw new ConcurrencyConflictException(message: "Conflict: the aggregate was modified by another request.", id: aggregateId);
		}
	}

	public async Task SaveAsync(
		Guid aggregateId,
		string aggregateType,
		IEnumerable<IEvent> events,
		int expectedVersion,
		Func<string>? snapshotFactory = null,
		CancellationToken ct = default)
	{
		List<IEvent> eventList = [.. events];
		if (eventList.Count == 0)
			return;

		using Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(
			name: FinanceTrackerActivitySource.Operations.EventStoreSave,
			kind: ActivityKind.Client
		);

		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.AggregateId, value: aggregateId);
		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.AggregateType, value: aggregateType);
		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.EventsCount, value: eventList.Count);

		if (options.CurrentValue.PreValidateExpectedVersion)
		{
			await EnsureExpectedVersionAsync(
				aggregateId: aggregateId,
				aggregateType: aggregateType,
				expectedVersion: expectedVersion,
				ct: ct
			);
		}

		(List<EventEntity> entities, List<OutboxEventEnvelope> envelopes) = BuildEntities(
			aggregateId: aggregateId,
			aggregateType: aggregateType,
			eventList: eventList,
			expectedVersion: expectedVersion,
			now: dateProvider.UtcNow
		);

		await context.Events.AddRangeAsync(entities: entities, cancellationToken: ct);

		if (envelopes.Count > 0)
		{
			string payload = JsonSerializer.Serialize(value: new OutboxPayload(
				AggregateId: aggregateId,
				CorrelationId: correlationContext.CorrelationId,
				Events: envelopes
			), options: FinanceTrackerJsonOptions.Payload);

			await context.OutboxMessages.AddAsync(entity: new OutboxMessageEntity()
			{
				Id = Guid.CreateVersion7(),
				AggregateId = aggregateId,
				AggregateType = aggregateType,
				Payload = payload,
				UpdatedAt = dateProvider.UtcNow,
				ProcessedAt = null
			}, cancellationToken: ct);
		}

		await ApplySnapshot(
			aggregateId: aggregateId,
			aggregateType: aggregateType,
			expectedVersion: expectedVersion,
			snapshotFactory: snapshotFactory,
			eventsCount: eventList.Count,
			ct: ct
		);
	}

	public async Task<EventStoreResult> LoadAsync(
		Guid aggregateId,
		string aggregateType,
		CancellationToken ct = default)
	{
		using Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(
			name: FinanceTrackerActivitySource.Operations.EventStoreLoad,
			kind: ActivityKind.Client
		);

		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.AggregateId, value: aggregateId);
		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.AggregateType, value: aggregateType);

		SnapshotEntity? snapshot = await context.Snapshots.AsNoTracking()
			.Where(s => s.AggregateId == aggregateId && s.AggregateType == aggregateType)
			.OrderByDescending(s => s.Version)
			.FirstOrDefaultAsync(cancellationToken: ct);

		int fromVersion = snapshot?.Version ?? 0;

		List<EventEntity> entities = await context.Events.AsNoTracking()
			.Where(predicate: e => e.AggregateId == aggregateId && e.Version > fromVersion && e.AggregateType == aggregateType)
			.OrderBy(keySelector: e => e.Version)
			.ToListAsync(cancellationToken: ct);

		List<IEvent> domainEvents = new List<IEvent>(capacity: entities.Count);

		foreach (EventEntity entity in entities)
		{
			try
			{
				Type type = eventTypeResolver.ResolveType(typeName: entity.EventType);
				int currentVersion = eventTypeResolver.GetCurrentVersion(typeName: entity.EventType);
				int storedVersion = entity.SchemaVersion;

				IEvent domainEvent;

				if (storedVersion < currentVersion && upcasterRegistry.HasChain(eventType: entity.EventType))
				{
					domainEvent = upcasterRegistry.Apply(
						eventType: entity.EventType,
						payload: entity.Payload,
						storedVersion: storedVersion,
						currentVersion: currentVersion
					);
				}
				else
				{
					domainEvent = (IEvent)JsonSerializer.Deserialize(
						json: entity.Payload,
						returnType: type,
						options: FinanceTrackerJsonOptions.Payload
					)!;
				}

				domainEvents.Add(item: domainEvent);
			}
			catch (Exception ex)
			{
				logger.ZLogError(exception: ex, message: $"""
					Failed to deserialize event '{entity.EventType}' v{entity.SchemaVersion} (id: {entity.Id}) for {aggregateType} {aggregateId}.
					Stored at version {entity.Version}.
				""");
				throw;
			}
		}

		SnapshotData? snapshotData = snapshot is null ? null : new SnapshotData(
			AggregateId: snapshot.AggregateId,
			AggregateType: snapshot.AggregateType,
			Version: snapshot.Version,
			State: snapshot.State
		);

		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.SnapshotFound, value: snapshot is not null);
		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.EventsLoaded, value: entities.Count);

		return new EventStoreResult(Snapshot: snapshotData, Events: domainEvents);
	}

	public async IAsyncEnumerable<Guid> GetAggregateIdsAsync(
		string aggregateType,
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		IAsyncEnumerable<Guid> ids = context.Events.AsNoTracking().Where(predicate: e => e.AggregateType == aggregateType)
			.Select(selector: e => e.AggregateId)
			.Distinct()
			.AsAsyncEnumerable();

		await foreach (Guid id in ids.WithCancellation(cancellationToken: ct))
			yield return id;
	}
}
