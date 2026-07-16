using FinanceTracker.Worker.Shared.RabbitMQ.Connection;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

/// <summary>
/// Resolves the physical queue name a listener/audit service should bind to, shared between
/// <see cref="RabbitMqListenerService{TMessage,THandler}"/> and
/// <see cref="DeadLetterAuditListener{TMessage,THandler}"/> so both agree on the same queue —
/// and therefore the same <c>{queue}.dlx</c>/<c>{queue}.dlq</c> pair — for a given <c>THandler</c>.
/// </summary>
internal static class RabbitMqQueueNaming
{
	public static string Resolve<THandler>(RabbitMqOptions options)
	{
		if (options.QueueNameOverrides.TryGetValue(key: typeof(THandler).Name, out string? overrideName) && !String.IsNullOrWhiteSpace(value: overrideName))
			return overrideName;

		return options.QueueName ?? throw new InvalidOperationException(
			message: $"RabbitMQ:QueueName (or a QueueNameOverrides entry for '{typeof(THandler).Name}') must be configured."
		);
	}
}
