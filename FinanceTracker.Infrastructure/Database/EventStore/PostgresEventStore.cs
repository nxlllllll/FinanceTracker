using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.ES;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Core.Domains.Abstractions.ES.Upcast;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Tracing;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore.EventMapper;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Jobs.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class PostgresEventStore(
	FinanceTrackerContext context,
	IEventTypeResolver eventTypeResolver,
	IIntegrationEventMapper integrationEventMapper,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	IDateProvider dateProvider,
	ILogger<PostgresEventStore> logger,
	IOptions<EventStoreOptions> options,
	ICorrelationContext correlationContext,
	IEventUpcasterRegistry upcasterRegistry
) : IEventStore
{
	private (List<EventEntity> Entities, List<OutboxEventEnvelope> Envelopes) BuildEntities(
		Guid aggregateId,
		string aggregateType,
		List<IEvent> eventList,
		int expectedVersion,
		DateTime now)
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

			IAccountIntegrationEvent? integrationEvent = integrationEventMapper.Map(domainEvent: @event);

			(string outboxEventType, string outboxPayload) = (eventType, serialized);
			if (integrationEvent is not null)
			{
				outboxEventType = integrationEventTypeResolver.ResolveTypeName(eventType: integrationEvent.GetType());
				outboxPayload = JsonSerializer.Serialize(value: integrationEvent, inputType: integrationEvent.GetType(), options: FinanceTrackerJsonOptions.Payload);
			}

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
		int previousThreshold = expectedVersion / options.Value.SnapshotThreshold;
		int newThreshold = newVersion / options.Value.SnapshotThreshold;

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

	public async Task SaveAsync(
		Guid aggregateId,
		string aggregateType,
		IEnumerable<IEvent> events,
		int expectedVersion,
		Func<string>? snapshotFactory = null,
		CancellationToken ct = default)
	{
		List<IEvent> eventList = events.ToList();
		if (eventList.Count == 0)
			return;

		using Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(name: "eventstore.save", kind: ActivityKind.Client);

		activity?.SetTag(key: "aggregate.id", value: aggregateId);
		activity?.SetTag(key: "aggregate.type", value: aggregateType);
		activity?.SetTag(key: "events.count", value: eventList.Count);
		
		(List<EventEntity> entities, List<OutboxEventEnvelope> envelopes) = BuildEntities(
			aggregateId: aggregateId,
			aggregateType: aggregateType,
			eventList: eventList,
			expectedVersion: expectedVersion,
			now: dateProvider.UtcNow
		);
		
		string payload = JsonSerializer.Serialize(value: new OutboxPayload(
			AggregateId: aggregateId,
			CorrelationId: correlationContext.CorrelationId,
			Events: envelopes
		), options: FinanceTrackerJsonOptions.Payload);

		await context.Events.AddRangeAsync(entities: entities, cancellationToken: ct);
		await context.OutboxMessages.AddAsync(
			entity: new OutboxMessageEntity()
			{
				Id = Guid.CreateVersion7(),
				AggregateId = aggregateId,
				AggregateType = aggregateType,
				Payload = payload,
				UpdatedAt = dateProvider.UtcNow,
				ProcessedAt = null
			},
			cancellationToken: ct
		);

		await ApplySnapshot(
			aggregateId: aggregateId,
			aggregateType: aggregateType,
			expectedVersion: expectedVersion,
			snapshotFactory: snapshotFactory,
			eventsCount: eventList.Count,
			ct: ct
		);

		try
		{
			await context.SaveChangesAsync(cancellationToken: ct);
		}
		catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
		{
			logger.ZLogWarning(exception: exception, message: $"Concurrency conflict: {aggregateType} {aggregateId} was modified by another request.");
			throw new ConcurrencyConflictException(
				message: "Conflict: aggregate was modified by another request.",
				id: aggregateId
			);
		}
	}

	public async Task<EventStoreResult> LoadAsync(
		Guid aggregateId,
		string aggregateType,
		CancellationToken ct = default)
	{
		using Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(name: "eventstore.load", kind: ActivityKind.Client);

		activity?.SetTag(key: "aggregate.id", value: aggregateId);
		activity?.SetTag(key: "aggregate.type", value: aggregateType);
		
		SnapshotEntity? snapshot = await context.Snapshots.AsNoTracking()
			.Where(s => s.AggregateId == aggregateId && s.AggregateType == aggregateType)
			.OrderByDescending(s => s.Version)
			.FirstOrDefaultAsync(cancellationToken: ct);

		int fromVersion = snapshot?.Version ?? 0;

		List<EventEntity> entities = await context.Events.AsNoTracking()
			.Where(predicate: e => e.AggregateId == aggregateId
				&& e.Version > fromVersion
				&& e.AggregateType == aggregateType)
			.OrderBy(keySelector: e => e.Version)
			.ToListAsync(cancellationToken: ct);

		List<IEvent> domainEvents = entities.Select(selector: entity =>
		{
			Type type = eventTypeResolver.ResolveType(typeName: entity.EventType);
			int currentVersion = eventTypeResolver.GetCurrentVersion(typeName: entity.EventType);
			int storedVersion = entity.SchemaVersion;

			using JsonDocument raw = JsonDocument.Parse(json: entity.Payload);
			using JsonDocument upcasted = upcasterRegistry.Apply(
				eventType: entity.EventType,
				source: raw,
				storedVersion: storedVersion,
				currentVersion: currentVersion
			);

			return (IEvent)upcasted.RootElement.Deserialize(
				returnType: type,
				options: FinanceTrackerJsonOptions.Payload
			)!;
		}).ToList();

		SnapshotData? snapshotData = snapshot is null ? null : new SnapshotData(
			AggregateId: snapshot.AggregateId,
			AggregateType: snapshot.AggregateType,
			Version: snapshot.Version,
			State: snapshot.State
		);

		activity?.SetTag(key: "snapshot.found", value: snapshot is not null);
		activity?.SetTag(key: "events.loaded", value: entities.Count);
		
		return new EventStoreResult(Snapshot: snapshotData, Events: domainEvents);
	}
}