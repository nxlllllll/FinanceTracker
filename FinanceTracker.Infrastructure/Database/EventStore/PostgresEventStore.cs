using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
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
	
	public async Task SaveAsync(
		Guid aggregateId,
		string aggregateType,
		IEnumerable<IEvent> events,
		int expectedVersion,
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
		await context.OutboxMessages.AddAsync(entity: new OutboxMessageEntity()
		{
			Id = Guid.NewGuid(),
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			Payload = JsonSerializer.Serialize(value: new OutboxPayload(
				AggregateId: aggregateId,
				Events: envelopes
			)),
			CreatedAt = DateTime.UtcNow,
			ProcessedAt = null
		}, cancellationToken: ct);
		
		try
		{
			await context.SaveChangesAsync(cancellationToken: ct);
		}
		catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
		{
			throw new InvalidOperationException(
				message: $"Conflict: aggregate {aggregateId} was modified by another request. Please retry.");
		}
	}

	public async Task<IReadOnlyList<IEvent>> LoadAsync(
		Guid aggregateId,
		CancellationToken ct = default)
	{
		List<EventEntity> entities = await context.Events.AsNoTracking()
												.Where(predicate: @event => @event.AggregateId == aggregateId)
												.OrderBy(keySelector: @event => @event.Version)
												.ToListAsync(cancellationToken: ct);

		return entities.Select(selector: entity =>
		{
			Type type = eventTypeResolver.ResolveType(typeName: entity.EventType);
			return (IEvent)JsonSerializer.Deserialize(json: entity.Payload, returnType: type)!;
		}).ToList();
	}
}