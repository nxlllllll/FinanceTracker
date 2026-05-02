namespace FinanceTracker.Core.Domains.Abstractions;

[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class EventTypeAttribute(string name) : Attribute
{
	public string Name { get; } = name;
}