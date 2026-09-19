namespace FinanceTracker.Infrastructure.Configurations;

public static class HealthCheckEndpoints
{
	public const string Live = "/health/live";
	public const string Ready = "/health/ready";
	public const string Metrics = "/health/metrics";
}
