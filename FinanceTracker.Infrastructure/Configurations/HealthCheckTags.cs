namespace FinanceTracker.Infrastructure.Configurations;

public static class HealthCheckTags
{
	public const string Ready = "ready";
	public const string Database = "db";
	public const string Cache = "cache";
	public const string Broker = "broker";
	public const string Scheduler = "scheduler";
	public const string External = "external";
}
