namespace FinanceTracker.Worker.Shared.RabbitMQ.Retry;

public interface IRetryCounter
{
	Task<int> IncrementAsync(string messageKey, CancellationToken ct = default);
	Task RemoveAsync(string messageKey, CancellationToken ct = default);
}