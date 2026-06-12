using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
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
		DateTimeOffset? cursor = null;
		int totalLogged = 0;

		while (true)
		{
			IReadOnlyList<UnresolvableEvent> batch = await unresolvableEventReadRepository.GetBatchAsync(
				batchSize: options.BatchSize,
				cursor: cursor,
				ct: ct
			);

			if (batch.Count == 0)
				break;

			WorkerMetrics.DeadLetterCount.Record(value: batch.Count);

			logger.ZLogWarning(message: $"Found {batch.Count} unresolvable event(s) requiring manual intervention (cursor: {cursor:O}).");

			foreach (UnresolvableEvent @event in batch)
				logger.ZLogWarning(message: $"Unresolvable event: Id={@event.Id}, Type={@event.Type}, ReferenceId={@event.ReferenceId}, Reason={@event.Reason}, OccurredAt={@event.OccurredAt:O}.");

			totalLogged += batch.Count;
			cursor = batch[^1].OccurredAt;

			if (batch.Count < options.BatchSize)
				break;
		}

		if (totalLogged > 0)
			logger.ZLogWarning(message: $"Dead letter scan complete. Total unresolvable events logged: {totalLogged}.");
	}
}