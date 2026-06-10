namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class UpcasterVersionAttribute(int from, int to) : Attribute
{
	public int From { get; } = from;
	public int To { get; } = to;
}