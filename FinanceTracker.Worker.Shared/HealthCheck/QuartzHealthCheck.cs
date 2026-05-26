using Microsoft.Extensions.Diagnostics.HealthChecks;
using Quartz;

namespace FinanceTracker.Worker.Shared.HealthCheck;

public sealed class QuartzHealthCheck(ISchedulerFactory schedulerFactory) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
	{
		IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken: ct);

		if (scheduler is { IsStarted: true, IsShutdown: false })
			return HealthCheckResult.Healthy();
		
		return HealthCheckResult.Unhealthy(description: "Quartz scheduler is not running.");
	}
}
