namespace FinanceTracker.Contracts.Messages.Domain;

public sealed record DomainEventMessage(
	Guid MessageId,
	string EventType,
	Guid AggregateId,
	string AggregateType,
	Guid? CorrelationId,
	string Payload,
	DateTimeOffset OccurredAt
) : IRoutableMessage
{
	public string RoutingKey => AggregateType;
}
