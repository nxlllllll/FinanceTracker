using FinanceTracker.Contracts.Messages;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Publisher;

/// <summary>
/// Publishes messages to a RabbitMQ topic exchange.
/// The routing key is derived from <see cref="IRoutableMessage.RoutingKey"/>.
/// </summary>
public interface IRabbitMqPublisher : IAsyncDisposable
{
	/// <summary>
	/// Serializes and publishes <paramref name="message"/> to the configured exchange.
	/// Sets <c>CorrelationId</c> in message properties for distributed tracing.
	/// </summary>
	Task PublishAsync<TMessage>(
		TMessage message,
		Guid? correlationId = default,
		CancellationToken ct = default
	) where TMessage : class, IRoutableMessage;
}
