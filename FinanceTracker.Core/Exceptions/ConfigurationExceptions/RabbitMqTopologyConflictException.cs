namespace FinanceTracker.Core.Exceptions.ConfigurationExceptions;

public sealed class RabbitMqTopologyConflictException(
	string message,
	string queueName,
	string brokerReply
) : ConfigurationException(message: message)
{
	public string QueueName { get; init; } = queueName;
	public string BrokerReply { get; init; } = brokerReply;
}
