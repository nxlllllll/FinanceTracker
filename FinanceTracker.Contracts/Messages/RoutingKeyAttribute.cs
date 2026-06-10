namespace FinanceTracker.Contracts.Messages;

/// <summary>
/// Declares the RabbitMQ routing key for a message class.
/// Read by <c>RabbitMqListenerService</c> at startup to bind the queue to the exchange.
/// Must be placed on every <see cref="IRoutableMessage"/> implementation.
/// </summary>
[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class RoutingKeyAttribute(string routingKey) : Attribute
{
	/// <summary>The routing key used when binding this message type to a queue.</summary>
	public string RoutingKey { get; } = routingKey;
}