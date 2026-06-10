using FinanceTracker.Core.Domains.Abstractions.Aggregate;

namespace FinanceTracker.Contracts.Messages.Account;

/// <summary>
/// Integration message published to RabbitMQ after each successful <c>IEventStore.SaveAsync</c>.
/// Carries the serialized domain events for a single aggregate write so that projection
/// workers (e.g. <c>AccountProjectionWorker</c>) can update read models asynchronously.
/// </summary>
[RoutingKey(routingKey: AggregateTypeNames.Account)]
public sealed record AggregateEventsMessage(
	Guid MessageId,
	Guid AggregateId,
	string AggregateType,
	Guid CorrelationId,
	IReadOnlyList<EventEnvelope> Events
) : IRoutableMessage
{
	/// <inheritdoc/>
	public string RoutingKey => AggregateType;
}