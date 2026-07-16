namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class EventVersionAttribute(int version) : Attribute
{
	public int Version { get; } = version;
}
