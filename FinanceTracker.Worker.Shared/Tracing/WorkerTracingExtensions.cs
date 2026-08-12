using FinanceTracker.Core.Observability.Tracing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FinanceTracker.Worker.Shared.Tracing;

/// <summary>
/// Extension methods for registering OpenTelemetry tracing in worker services.
/// Call <see cref="AddWorkerTracing"/> in each worker's <c>Program.cs</c> to configure
/// the OTLP exporter and wire in the <see cref="FinanceTrackerActivitySource"/>.
/// </summary>
public static class WorkerTracingExtensions
{
	/// <summary>
	/// Registers OpenTelemetry tracing with the OTLP exporter.
	/// </summary>
	/// <param name="workerName">Service name reported in traces (e.g. <c>"worker-outbox"</c>).</param>
	public static IServiceCollection AddWorkerTracing(
		this IServiceCollection services,
		string workerName)
	{
		services.AddOpenTelemetry()
			.WithTracing(configure: builder => builder.SetResourceBuilder(resourceBuilder: ResourceBuilder.CreateDefault().AddService(serviceName: workerName))
			.AddSource(names: FinanceTrackerActivitySource.Name)
			.AddOtlpExporter()
		);

		return services;
	}
}
