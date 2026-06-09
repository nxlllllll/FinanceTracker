using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Connection;

public sealed record RabbitMqOptions
{
	public const string SectionName = "RabbitMQ";

	public string Host { get; init; } = "localhost";
	public int Port { get; init; } = 5672;
	public string Username { get; init; } = "guest";
	public string Password { get; init; } = "guest";
	public string ExchangeName { get; init; } = "finance-tracker";
	public string? QueueName { get; init; }

	[Range(minimum: 1, maximum: 100)]
	public int MaxRetries { get; init; } = 3;

	[Range(minimum: 1, maximum: 168)]
	public int RetryCounterTtlHours { get; init; } = 24;
}