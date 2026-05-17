using FinanceTracker.Core.Tracing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FinanceTracker.Worker.Shared.Tracing;

public static class WorkerTracingExtensions
{
	public static IServiceCollection AddWorkerTracing(
		this IServiceCollection services,
		string workerName)
	{
		services.AddOpenTelemetry().WithTracing(builder => builder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: workerName))
			.AddSource(names: FinanceTrackerActivitySource.Name)
			.AddOtlpExporter()
		);

		return services;
	}
}