namespace FinanceTracker.Contracts.Events;

/// <summary>
/// Links an integration event class to its corresponding domain event type.
/// Used by architecture tests to verify that every domain event has a mapped
/// integration event, and by <c>IntegrationEventTypeResolver</c> to resolve
/// types at runtime during outbox publishing.
/// </summary>
[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventTypeAttribute(Type domainEventType) : Attribute
{
	/// <summary>The domain event type this integration event represents.</summary>
	public Type DomainEventType { get; } = domainEventType;
}