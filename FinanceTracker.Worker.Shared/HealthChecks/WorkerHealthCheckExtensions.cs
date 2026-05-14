using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace FinanceTracker.Worker.Shared.HealthChecks;

public static class WorkerHealthCheckExtensions
{
	public static IHealthChecksBuilder AddWorkerHealthChecks(this IServiceCollection services, string connectionString)
	{
		return services.AddHealthChecks().AddNpgSql(
			connectionString: connectionString,
			name: "postgres",
			tags: ["ready", "db"]
		);
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