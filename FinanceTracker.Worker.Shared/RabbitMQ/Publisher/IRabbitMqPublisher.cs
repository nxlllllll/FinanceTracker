using FinanceTracker.Contracts.Messages;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Publisher;

public interface IRabbitMqPublisher : IAsyncDisposable
{
	Task PublishAsync<TMessage>(
		TMessage message,
		Guid? correlationId = default,
		CancellationToken ct = default
	) where TMessage : class, IRoutableMessage;
}
