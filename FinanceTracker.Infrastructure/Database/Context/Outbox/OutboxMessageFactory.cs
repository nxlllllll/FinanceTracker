using System.Diagnostics;
using System.Text.Json;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Observability.Tracing;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

namespace FinanceTracker.Infrastructure.Database.Context.Outbox;

internal static class OutboxMessageFactory
{
	public static OutboxEventEnvelope CreateEnvelope(
		IIntegrationEvent integrationEvent,
		IIntegrationEventTypeResolver integrationEventTypeResolver)
	{
		string eventType = integrationEventTypeResolver.ResolveTypeName(eventType: integrationEvent.GetType());
		string payload = JsonSerializer.Serialize(value: integrationEvent, inputType: integrationEvent.GetType(), options: FinanceTrackerJsonOptions.Payload);

		return new OutboxEventEnvelope(EventType: eventType, EventPayload: payload);
	}

	public static OutboxMessageEntity CreateMessage(
		Guid aggregateId,
		string aggregateType,
		Guid correlationId,
		IReadOnlyList<OutboxEventEnvelope> envelopes,
		DateTimeOffset now)
	{
		string payload = JsonSerializer.Serialize(value: new OutboxPayload(
			AggregateId: aggregateId,
			CorrelationId: correlationId,
			Events: envelopes,
			TraceParent: FinanceTrackerActivitySource.CaptureTraceParent(),
			TraceState: Activity.Current?.TraceStateString
		), options: FinanceTrackerJsonOptions.Payload);

		return new OutboxMessageEntity()
		{
			Id = Guid.CreateVersion7(),
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			Payload = payload,
			UpdatedAt = now,
			ProcessedAt = null
		};
	}
}
