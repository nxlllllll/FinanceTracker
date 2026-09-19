using System.Net;
using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Shared.Metrics;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
		return services.AddHealthChecks().AddNpgSql(connectionString: connectionString, name: HealthCheckNames.Postgres, tags: [HealthCheckTags.Ready, HealthCheckTags.Database]).AddRedis(
			redisConnectionString: redisConnectionString,
			name: HealthCheckNames.Redis,
			failureStatus: HealthStatus.Degraded,
			tags: [HealthCheckTags.Ready, HealthCheckTags.Cache],
			timeout: TimeSpan.FromSeconds(value: 2)
		);
	}

	public static IServiceCollection AddWorkerMetrics(this IServiceCollection services, string workerName)
	{
		services.AddOpenTelemetry().WithMetrics(configure: builder =>
		{
			builder.SetResourceBuilder(resourceBuilder: ResourceBuilder.CreateDefault().AddService(serviceName: workerName))
				.AddMeter(names: WorkerMetrics.MeterName)
				.AddMeter(names: FinanceTrackerMetrics.MeterName)
				.AddRuntimeInstrumentation()
				.AddPrometheusExporter();
		});

		return services;
	}

	public static WebApplication MapWorkerEndpoints(this WebApplication app)
	{
		app.MapHealthChecks(pattern: HealthCheckEndpoints.Live, options: new HealthCheckOptions
		{
			Predicate = _ => false
		});

		app.MapHealthChecks(pattern: HealthCheckEndpoints.Ready, options: new HealthCheckOptions
		{
			Predicate = check => check.Tags.Contains(item: HealthCheckTags.Ready),
			ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
		});

		app.UseHealthChecksPrometheusExporter(
			endpoint: HealthCheckEndpoints.Metrics,
			configure: options => options.ResultStatusCodes[HealthStatus.Unhealthy] = (int)HttpStatusCode.OK
		);

		app.MapPrometheusScrapingEndpoint();

		return app;
	}
}
