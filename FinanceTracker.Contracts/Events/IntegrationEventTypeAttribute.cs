namespace FinanceTracker.Contracts.Events;

[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventTypeAttribute(Type domainEventType) : Attribute
{
	public Type DomainEventType { get; } = domainEventType;
}
