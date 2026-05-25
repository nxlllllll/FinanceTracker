namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

public interface IMessageHandler<in TMessage> where TMessage : class
{
	Task HandleAsync(TMessage message, CancellationToken ct = default);
}
