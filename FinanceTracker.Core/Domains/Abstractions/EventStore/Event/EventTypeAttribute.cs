namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class EventTypeAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}
