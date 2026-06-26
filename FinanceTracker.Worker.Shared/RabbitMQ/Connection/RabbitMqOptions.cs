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
	/// Optional per-handler queue name overrides, keyed by handler type name (e.g. <c>"AccountEventsConsumer"</c>).
	/// In production each worker process hosts exactly one listener, so <see cref="QueueName"/> alone is enough.
	/// Test hosts (or any process that hosts multiple listeners against a single <see cref="RabbitMqOptions"/>
	/// section) must give each handler its own queue here — otherwise multiple listeners would become competing
	/// consumers on the same physical queue and silently steal each other's messages.
	/// </summary>
	public Dictionary<string, string> QueueNameOverrides { get; } = new Dictionary<string, string>();

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

	/// <summary>
	/// Maximum number of unacknowledged messages the broker will deliver to this consumer at once
	/// (<c>basic.qos</c> prefetch count). Bounds how much work piles up client-side, keeps delivery
	/// fair across horizontally scaled replicas of the same worker, and limits how many messages get
	/// requeued at once if this process crashes mid-batch. Default: 10.
	/// </summary>
	[Range(minimum: 1, maximum: 1000)]
	public int PrefetchCount { get; init; } = 10;
}