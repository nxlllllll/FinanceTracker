namespace FinanceTracker.Core.Domains.Abstractions.ES.Upcast;

[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class EventVersionAttribute(int version) : Attribute
{
	public int Version { get; } = version;
}