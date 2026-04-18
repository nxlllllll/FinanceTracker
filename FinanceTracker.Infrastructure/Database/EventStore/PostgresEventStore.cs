using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class PostgresEventStore(
	FinanceTrackerContext context,
	IEventTypeRegistry eventTypeRegistry
) : IEventStore
{
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
		
		int currentVersion = expectedVersion;
		List<EventEntity> entities = eventList.Select(selector: @event => new EventEntity()
		{
			Id = @event.Id,
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			EventType = @event.GetType().Name,
			Version = ++currentVersion,
			Payload = JsonSerializer.Serialize(value: @event, inputType: @event.GetType()),
			OccurredAt = @event.OccurredAt,
			CreatedAt = DateTime.UtcNow
		}).ToList();
		
		await context.Events.AddRangeAsync(entities: entities, cancellationToken: ct);

		try
		{
			await context.SaveChangesAsync(cancellationToken: ct);
		}
		catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
		{
			throw new InvalidOperationException(message: $"Conflict: aggregate {aggregateId} was modified by another request. Please retry.");
		}
	}

	public async Task<IReadOnlyList<IEvent>> LoadAsync(
		Guid aggregateId,
		CancellationToken ct = default)
	{
		List<EventEntity> entities = await context.Events
			.Where(predicate: @event => @event.AggregateId == aggregateId)
			.OrderBy(keySelector: @event => @event.Version)
			.ToListAsync(cancellationToken: ct);
		
		return entities.Select(selector: entity =>
		{
			Type type = eventTypeRegistry.ResolveType(typeName: entity.EventType);
			return (IEvent)JsonSerializer.Deserialize(json: entity.Payload, returnType: type)!;			
		}).ToList();
	}
}