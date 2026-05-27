namespace FinanceTracker.Contracts.Messages;

[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class RoutingKeyAttribute(string routingKey) : Attribute
{
	public string RoutingKey { get; } = routingKey;
}