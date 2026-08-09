using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinanceTracker.Infrastructure.Configurations;

public static class InfrastructureHealthCheckExtensions
{
	public static IHealthChecksBuilder AddInfrastructureHealthChecks(
		this IServiceCollection services,
		string connectionString,
		string redisConnectionString)
	{
		return services.AddHealthChecks().AddNpgSql(connectionString: connectionString, name: "postgres", tags: ["ready", "db"]).AddRedis(
			redisConnectionString: redisConnectionString,
			name: "redis",
			failureStatus: HealthStatus.Degraded,
			tags: ["ready", "cache"],
			timeout: TimeSpan.FromSeconds(value: 2)
		);
	}
}
