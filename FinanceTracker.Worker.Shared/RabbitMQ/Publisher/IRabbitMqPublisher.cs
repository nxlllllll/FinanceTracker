namespace FinanceTracker.Worker.Shared.RabbitMQ.Publish;

public interface IRabbitMqPublisher : IAsyncDisposable
{
	Task PublishAsync<TMessage>(
		TMessage message,
		CancellationToken ct = default
	) where TMessage : class;
}