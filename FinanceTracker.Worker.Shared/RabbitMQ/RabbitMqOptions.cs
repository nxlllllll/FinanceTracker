namespace FinanceTracker.Worker.Shared.RabbitMQ;

public sealed class RabbitMqOptions
{
	public const string SectionName = "RabbitMQ";

	public string Host { get; init; } = "localhost";
	public int Port { get; init; } = 5672;
	public string Username { get; init; } = "guest";
	public string Password { get; init; } = "guest";
	public string ExchangeName { get; init; } = "finance-tracker";
	public string? QueueName { get; init; }
}