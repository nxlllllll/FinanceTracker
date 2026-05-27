using FinanceTracker.Core.Domains.Abstractions.Aggregate;

namespace FinanceTracker.Contracts.Messages.Account;

[RoutingKey(routingKey: AggregateTypeNames.Account)]
public sealed record AggregateEventsMessage(
	Guid MessageId,
	Guid AggregateId,
	string AggregateType,
	Guid CorrelationId,
	IReadOnlyList<EventEnvelope> Events
) : IRoutableMessage
{
	public string RoutingKey => AggregateType;
}