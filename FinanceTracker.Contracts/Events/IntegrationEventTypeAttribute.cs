namespace FinanceTracker.Contracts.Events;

/// <summary>
/// Names an integration event in the outbox envelope, either through the domain event it represents
/// or directly. Used by architecture tests to verify that every domain event has a mapped
/// integration event, and by <c>IntegrationEventTypeResolver</c> to resolve types at runtime
/// during outbox publishing.
/// </summary>
[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventTypeAttribute : Attribute
{
	/// <summary>For an event published from an aggregate's stream: the domain event it represents.</summary>
	public IntegrationEventTypeAttribute(Type domainEventType) => DomainEventType = domainEventType;

	/// <summary>For an event published by something that keeps no stream, and so has no domain event to take a name from.</summary>
	public IntegrationEventTypeAttribute(string name) => Name = name;

	/// <summary>The domain event type this integration event represents, if it has one.</summary>
	public Type? DomainEventType { get; }

	/// <summary>The name carried in the outbox envelope, if it was given directly.</summary>
	public string? Name { get; }
}
