using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace FinanceTracker.Worker.Shared.HealthCheck;

/// <summary>
/// Extension methods for registering standard worker health checks and OpenTelemetry metrics.
/// Call <see cref="AddWorkerHealthChecks"/> in each worker's <c>Program.cs</c> to expose
/// a <c>/health</c> endpoint covering Postgres, Redis, Quartz, and RabbitMQ.
/// </summary>
public static class WorkerHealthCheckExtensions
{
	/// <summary>
	/// Registers health checks for Postgres and Redis.
	/// Add <c>.AddQuartzHealthChecks()</c> and <c>.AddRabbitMqHealthCheck()</c>
	/// to the returned builder for workers that use those dependencies.
	/// </summary>
	public static IHealthChecksBuilder AddWorkerHealthChecks(
		this IServiceCollection services,
		string connectionString,
		string redisConnectionString)
	{
		return services.AddHealthChecks().AddNpgSql(connectionString: connectionString, name: "postgres", tags: ["ready", "db"])
			.AddRedis(redisConnectionString: redisConnectionString, name: "redis", tags: ["ready", "cache"]);
	}

	public static IServiceCollection AddWorkerMetrics(this IServiceCollection services, string workerName)
	{
		services.AddOpenTelemetry().WithMetrics(configure: builder =>
		{
			builder.SetResourceBuilder(resourceBuilder: ResourceBuilder.CreateDefault().AddService(serviceName: workerName))
				.AddMeter(names: WorkerMetrics.MeterName)
				.AddRuntimeInstrumentation()
				.AddPrometheusExporter();
		});

		return services;
	}

	public static WebApplication MapWorkerEndpoints(this WebApplication app)
	{
		app.MapHealthChecks(pattern: "/health/live", options: new HealthCheckOptions
		{
			Predicate = _ => false
		});

		app.MapHealthChecks(pattern: "/health/ready", options: new HealthCheckOptions
		{
			Predicate = check => check.Tags.Contains(item: "ready")
		});

		app.MapPrometheusScrapingEndpoint();

		return app;
	}
}
