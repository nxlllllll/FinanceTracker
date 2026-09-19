using FinanceTracker.Infrastructure.Database.EventStore;
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
		return services.AddHealthChecks()
			.AddNpgSql(connectionString: connectionString, name: HealthCheckNames.Postgres, tags: [HealthCheckTags.Ready, HealthCheckTags.Database])
			.AddCheck<EventSchemaHealthCheck>(name: HealthCheckNames.EventSchema, tags: [HealthCheckTags.Ready, HealthCheckTags.Database])
			.AddRedis(
				redisConnectionString: redisConnectionString,
				name: HealthCheckNames.Redis,
				failureStatus: HealthStatus.Degraded,
				tags: [HealthCheckTags.Ready, HealthCheckTags.Cache],
				timeout: TimeSpan.FromSeconds(value: 2)
			);
	}
}
