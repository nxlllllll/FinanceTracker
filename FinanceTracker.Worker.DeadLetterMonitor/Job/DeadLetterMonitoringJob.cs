using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.DeadLetterMonitor.Job;

[DisallowConcurrentExecution]
public sealed class DeadLetterMonitoringJob(
	IUnresolvableEventReadRepository unresolvableEventReadRepository,
	IOptionsMonitor<DeadLetterMonitoringOptions> options,
	ILogger<DeadLetterMonitoringJob> logger
) : BaseJob<DeadLetterMonitoringOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(DeadLetterMonitoringOptions options, CancellationToken ct)
	{
		IReadOnlyList<UnresolvableEvent> events = await unresolvableEventReadRepository.GetAllAsync(ct: ct);

		WorkerMetrics.DeadLetterCount.Record(value: events.Count);

		if (events.Count == 0)
			return;

		logger.ZLogWarning(message: $"Found {events.Count} unresolvable event(s) requiring manual intervention.");

		foreach (UnresolvableEvent @event in events)
			logger.ZLogWarning(message: $"Unresolvable event: Id={@event.Id}, Type={@event.Type}, ReferenceId={@event.ReferenceId}, Reason={@event.Reason}, OccurredAt={@event.OccurredAt:O}.");
	}
}