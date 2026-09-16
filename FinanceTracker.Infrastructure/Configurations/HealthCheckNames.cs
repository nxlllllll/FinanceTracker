namespace FinanceTracker.Infrastructure.Configurations;

public static class HealthCheckNames
{
	public const string Postgres = "postgres";
	public const string Redis = "redis";
	public const string EventSchema = "event-schema";
	public const string RabbitMq = "rabbitmq";
	public const string Quartz = "quartz";
}
