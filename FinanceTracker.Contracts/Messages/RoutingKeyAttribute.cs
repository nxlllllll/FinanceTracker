namespace FinanceTracker.Contracts.Messages;

/// <summary>
/// Declares the RabbitMQ routing key a message <em>handler</em> is interested in.
/// Read by <c>RabbitMqListenerService</c> at startup to bind that handler's queue to the exchange.
/// Placed on <see cref="IMessageHandler{TMessage}"/> implementations, not on message types — several
/// handlers can share one message shape (see <see cref="AggregateEventsMessage"/>) while each caring
/// about a different routing key.
/// </summary>
[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class RoutingKeyAttribute(string routingKey) : Attribute
{
	/// <summary>The routing key used when binding this handler's queue to the exchange.</summary>
	public string RoutingKey { get; } = routingKey;
}
