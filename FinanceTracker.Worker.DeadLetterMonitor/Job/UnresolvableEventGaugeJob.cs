using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;

namespace FinanceTracker.Worker.DeadLetterMonitor.Job;

/// <summary>
/// Publishes the pending-escalation count often enough for alerting to be timely. This is the only
/// writer of <c>unresolvable_events_pending</c>, which two Prometheus alerts and the overview
/// dashboard read — <see cref="DeadLetterBacklogSummaryJob"/> runs daily and cannot stand in for it.
/// </summary>
[DisallowConcurrentExecution]
public sealed class UnresolvableEventGaugeJob(
	IUnresolvableEventReadRepository unresolvableEventReadRepository,
	IOptionsMonitor<UnresolvableEventGaugeOptions> options,
	ILogger<UnresolvableEventGaugeJob> logger
) : BaseJob<UnresolvableEventGaugeOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(UnresolvableEventGaugeOptions options, CancellationToken ct)
	{
		int stillUnresolved = await unresolvableEventReadRepository.CountUnresolvedAsync(ct: ct);

		WorkerMetrics.UnresolvableEventsPending.Record(value: stillUnresolved);
	}
}
