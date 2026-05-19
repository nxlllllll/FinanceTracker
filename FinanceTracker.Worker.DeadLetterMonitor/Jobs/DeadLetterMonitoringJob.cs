using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Worker.Shared.Metrics;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.DeadLetterMonitor.Jobs;

[DisallowConcurrentExecution]
public sealed class DeadLetterMonitoringJob(
	IUnresolvableEventReadRepository unresolvableEventReadRepository,
	ILogger<DeadLetterMonitoringJob> logger
) : IJob
{
	public async Task Execute(IJobExecutionContext executionContext)
	{
		IReadOnlyList<UnresolvableEventDto> events = await unresolvableEventReadRepository.GetAllAsync(ct: executionContext.CancellationToken);

		WorkerMetrics.DeadLetterCount.Record(value: events.Count);

		if (events.Count == 0)
			return;

		logger.ZLogWarning(message: $"Found {events.Count} unresolvable event(s) requiring manual intervention.");

		foreach (UnresolvableEventDto @event in events)
			logger.ZLogWarning(message: $"Unresolvable event: Id={@event.Id}, Type={@event.Type}, ReferenceId={@event.ReferenceId}, Reason={@event.Reason}, OccurredAt={@event.OccurredAt:O}.");
	}
}