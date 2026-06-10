using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Connection;

/// <summary>
/// Configuration for RabbitMQ connection and consumer behaviour.
/// Bind from <c>appsettings.json</c> under the <c>"RabbitMQ"</c> section.
/// Each worker that consumes from a queue provides its own section with a unique <see cref="QueueName"/>.
/// </summary>
public sealed record RabbitMqOptions
{
	public const string SectionName = "RabbitMQ";

	/// <summary>RabbitMQ broker hostname. Default: <c>localhost</c>.</summary>
	public string Host { get; init; } = "localhost";

	/// <summary>RabbitMQ broker port. Default: <c>5672</c>.</summary>
	public int Port { get; init; } = 5672;

	public string Username { get; init; } = "guest";
	public string Password { get; init; } = "guest";

	/// <summary>Topic exchange name. Must match across publishers and consumers. Default: <c>finance-tracker</c>.</summary>
	public string ExchangeName { get; init; } = "finance-tracker";

	/// <summary>Queue this consumer binds to. Required for listener services.</summary>
	public string? QueueName { get; init; }

	/// <summary>
	/// Maximum number of handler attempts before a message is sent to the dead-letter exchange
	/// and recorded in <c>unresolvable_events</c>. Default: 3.
	/// </summary>
	[Range(minimum: 1, maximum: 100)]
	public int MaxRetries { get; init; } = 3;

	/// <summary>
	/// How long the per-message retry counter is retained in Redis after the last attempt.
	/// Prevents stale keys accumulating for messages that are eventually acked. Default: 24 hours.
	/// </summary>
	[Range(minimum: 1, maximum: 168)]
	public int RetryCounterTtlHours { get; init; } = 24;
}