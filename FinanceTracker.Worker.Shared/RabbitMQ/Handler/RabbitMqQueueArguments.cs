namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

/// <summary>
/// Argument names RabbitMQ itself defines for queue declarations.
/// </summary>
internal static class RabbitMqQueueArguments
{
	internal const string QueueType = "x-queue-type";
	internal const string DeliveryLimit = "x-delivery-limit";
	internal const string DelayedRetryType = "x-delayed-retry-type";
	internal const string DelayedRetryMin = "x-delayed-retry-min";
	internal const string DelayedRetryMax = "x-delayed-retry-max";
	internal const string DeadLetterExchange = "x-dead-letter-exchange";

	/// <summary>Queue type this project uses everywhere: replicated, with native delivery limits.</summary>
	internal const string QuorumQueueType = "quorum";

	/// <summary>Retry mode that applies backoff only to deliveries that actually failed.</summary>
	internal const string FailedRetryType = "failed";
}
