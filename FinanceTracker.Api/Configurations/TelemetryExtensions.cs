using FinanceTracker.Core.Services.Metrics;
using FinanceTracker.Core.Services.Tracing;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FinanceTracker.Api.Configurations;

public static class TelemetryExtensions
{
	private const string ServiceName = "Api";

	public static IServiceCollection AddApiTelemetry(this IServiceCollection services)
	{
		ResourceBuilder resource = ResourceBuilder.CreateDefault().AddService(serviceName: ServiceName);

		services.AddOpenTelemetry().WithMetrics(configure: builder =>
		{
			builder.SetResourceBuilder(resourceBuilder: resource)
				.AddAspNetCoreInstrumentation()
				.AddHttpClientInstrumentation()
				.AddRuntimeInstrumentation()
				.AddMeter(names: FinanceTrackerMetrics.MeterName)
				.AddPrometheusExporter();
		});

		services.AddOpenTelemetry().WithTracing(configure: builder =>
		{
			builder.SetResourceBuilder(resourceBuilder: resource)
				.AddAspNetCoreInstrumentation()
				.AddHttpClientInstrumentation()
				.AddSource(names: FinanceTrackerActivitySource.Name)
				.AddOtlpExporter();
		});

		return services;
	}
}
