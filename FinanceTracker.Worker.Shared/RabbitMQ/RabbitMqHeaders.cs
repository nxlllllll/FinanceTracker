namespace FinanceTracker.Worker.Shared.RabbitMQ;

public static class RabbitMqHeaders
{
	/// <summary>
	/// When the outbox publisher handed the message to the broker, as Unix milliseconds.
	/// </summary>
	public const string PublishedAt = "x-ft-published-at";
}
