using FinanceTracker.Core.Domains.Abstractions.Aggregate;

namespace FinanceTracker.Contracts.Messages.Account;

/// <summary>
/// Integration message published to RabbitMQ after each successful <c>IEventStore.SaveAsync</c>.
/// </summary>
[RoutingKey(routingKey: AggregateTypeNames.Account)]
public sealed record AccountEventsMessage(
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
