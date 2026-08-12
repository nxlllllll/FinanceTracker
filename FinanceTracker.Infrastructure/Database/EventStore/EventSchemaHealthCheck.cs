using FinanceTracker.Core.Services.EventStore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinanceTracker.Infrastructure.Database.EventStore;

/// <summary>
/// Reports whether this process can still read what is in the event store.
/// </summary>
public sealed class EventSchemaHealthCheck(
	IEventSchemaHealthState state
) : IHealthCheck
{
	public Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken ct = default)
	{
		if (state.IsCompatible)
			return Task.FromResult(HealthCheckResult.Healthy(description: "Every event read so far matched a schema version this build understands."));

		return Task.FromResult(HealthCheckResult.Unhealthy(description: state.Diagnosis ?? "Encountered an event whose schema version this build cannot read."));
	}
}
