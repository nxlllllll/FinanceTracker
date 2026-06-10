namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

/// <summary>
/// Processes a deserialized RabbitMQ message of type <typeparamref name="TMessage"/>.
/// Implement this interface to handle a specific message type in a worker service.
/// Registration is done via <c>AddRabbitMqListener&lt;TMessage, THandler&gt;</c> in DI.
/// </summary>
public interface IMessageHandler<in TMessage> where TMessage : class
{
	/// <summary>
	/// Processes the message. Throw any exception to trigger the retry/dead-letter flow
	/// managed by <c>RabbitMqListenerService</c>.
	/// </summary>
	Task HandleAsync(TMessage message, CancellationToken ct = default);
}