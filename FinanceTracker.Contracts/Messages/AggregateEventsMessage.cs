namespace FinanceTracker.Contracts.Messages;

/// <summary>
/// Integration message published to RabbitMQ after each successful <c>IEventStore.SaveAsync</c>,
/// for any event-sourced aggregate (currently Account and UserPermission). One shared shape —
/// which routing key a given consumer cares about is declared via <see cref="RoutingKeyAttribute"/>
/// on the <em>handler</em>, not on this message type, since several handlers with different
/// interests can consume this same shape (e.g. account projection vs. transfer completion).
/// </summary>
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
