using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Outbox;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class PostgresEventStore(
	FinanceTrackerContext context,
	IEventTypeResolver eventTypeResolver
) : IEventStore
{
	private const int SnapshotThreshold = 50;
	
	private static (List<EventEntity> Entities, List<OutboxEventEnvelope> Envelopes) BuildEntities(
		Guid aggregateId,
		string aggregateType,
		List<IEvent> eventList,
		int expectedVersion)
	{
		int currentVersion = expectedVersion;
		List<EventEntity> entities = new List<EventEntity>(capacity: eventList.Count);
		List<OutboxEventEnvelope> envelopes = new List<OutboxEventEnvelope>(capacity: eventList.Count);

		foreach (IEvent @event in eventList)
		{
			string serialized = JsonSerializer.Serialize(value: @event, inputType: @event.GetType());
			string eventType = @event.GetType().Name;

			entities.Add(item: new EventEntity()
			{
				Id = @event.Id,
				AggregateId = aggregateId,
				AggregateType = aggregateType,
				EventType = eventType,
				Version = ++currentVersion,
				Payload = serialized,
				OccurredAt = @event.OccurredAt,
				CreatedAt = DateTime.UtcNow
			});

			envelopes.Add(item: new OutboxEventEnvelope(
				EventType: eventType,
				EventPayload: serialized
			));
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
		int previousThreshold = expectedVersion / SnapshotThreshold;
		int newThreshold = newVersion / SnapshotThreshold;

		if (snapshotFactory is null || newThreshold <= previousThreshold)
			return;

		SnapshotEntity? existing = await context.Snapshots.FirstOrDefaultAsync(
			predicate: s => s.AggregateId == aggregateId && s.AggregateType == aggregateType,
			cancellationToken: ct
		);

		string snapshot = snapshotFactory();
		
		if (existing is null)
		{
			await context.Snapshots.AddAsync(entity: new SnapshotEntity()
			{
				AggregateId = aggregateId,
				AggregateType = aggregateType,
				Version = newVersion,
				State = snapshot,
				CreatedAt = DateTime.UtcNow
			}, cancellationToken: ct);
		}
		else
		{
			existing.Version = newVersion;
			existing.State = snapshot;
			existing.CreatedAt = DateTime.UtcNow;
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
		List<IEvent> eventList = events.ToList();
		if (eventList.Count == 0)
			return;

		(List<EventEntity> entities, List<OutboxEventEnvelope> envelopes) = BuildEntities(
			aggregateId: aggregateId,
			aggregateType: aggregateType,
			eventList: eventList,
			expectedVersion: expectedVersion
		);

		await context.Events.AddRangeAsync(entities: entities, cancellationToken: ct);
		await context.OutboxMessages.AddAsync(
			entity: new OutboxMessageEntity() {
				Id = Guid.NewGuid(),
				AggregateId = aggregateId,
				AggregateType = aggregateType,
				Payload = JsonSerializer.Serialize(value: new OutboxPayload(
					AggregateId: aggregateId,
					Events: envelopes
				)),
				CreatedAt = DateTime.UtcNow,
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
			throw new ConcurrencyConflictException(message: $"Conflict: aggregate was modified by another request.", id: aggregateId);
		}
	}

	public async Task<EventStoreResult> LoadAsync(
		Guid aggregateId,
		CancellationToken ct = default)
	{
		SnapshotEntity? snapshot = await context.Snapshots.AsNoTracking().FirstOrDefaultAsync(
			predicate: s => s.AggregateId == aggregateId,
			cancellationToken: ct
		);

		int fromVersion = snapshot?.Version ?? 0;

		List<EventEntity> entities = await context.Events.AsNoTracking()
			.Where(predicate: e => e.AggregateId == aggregateId && e.Version > fromVersion)
			.OrderBy(keySelector: e => e.Version)
			.ToListAsync(cancellationToken: ct);

		List<IEvent> events = entities.Select(selector: entity =>
		{
			Type type = eventTypeResolver.ResolveType(typeName: entity.EventType);
			return (IEvent)JsonSerializer.Deserialize(json: entity.Payload, returnType: type)!;
		}).ToList();

		SnapshotData? snapshotData = snapshot is null ? null : new SnapshotData(
			AggregateId: snapshot.AggregateId,
			AggregateType: snapshot.AggregateType,
			Version: snapshot.Version,
			State: snapshot.State
		);

		return new EventStoreResult(Snapshot: snapshotData, Events: events);
	}
}