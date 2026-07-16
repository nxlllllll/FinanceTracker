namespace FinanceTracker.Contracts.Messages;

/// <summary>
/// Marks a message as publishable to a RabbitMQ topic exchange.
/// The <see cref="RoutingKey"/> determines which queues receive the message.
/// Annotate implementations with <see cref="RoutingKeyAttribute"/> to declare
/// the routing key statically — <c>RabbitMqListenerService</c> reads it at startup.
/// </summary>
public interface IRoutableMessage
{
	/// <summary>
	/// The RabbitMQ routing key used to route this message to bound queues.
	/// Typically, matches the aggregate type name (e.g. <c>"Account"</c>).
	/// </summary>
	string RoutingKey { get; }

	/// <summary>
	/// Unique identifier of this message instance.
	/// Used by consumers for idempotency deduplication via the
	/// <c>processed_messages</c> table — guarantees at-most-once processing
	/// even when the broker delivers the same message more than once.
	/// </summary>
	Guid MessageId { get; }
}
