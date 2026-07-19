using FinanceTracker.Core.Domains.Abstractions.Aggregate;

namespace FinanceTracker.Contracts.Messages.Permission;

/// <summary>
/// Integration message published to RabbitMQ after each successful <c>IEventStore.SaveAsync</c>
/// </summary>
[RoutingKey(routingKey: AggregateTypeNames.UserPermission)]
public sealed record PermissionEventsMessage(
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
