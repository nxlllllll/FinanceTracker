using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.DeadLetterMonitor.Job;

[DisallowConcurrentExecution]
public sealed class DeadLetterMonitoringJob(
	IUnresolvableEventReadRepository unresolvableEventReadRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IDateProvider dateProvider,
	IOptionsMonitor<DeadLetterMonitoringOptions> options,
	ILogger<DeadLetterMonitoringJob> logger
) : BaseJob<DeadLetterMonitoringOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(DeadLetterMonitoringOptions options, CancellationToken ct)
	{
		int totalLogged = 0;
		PagedResult<UnresolvableEvent> page;

		do
		{
			page = await unresolvableEventReadRepository.GetUnacknowledgedBatchAsync(
				batchSize: options.BatchSize,
				ct: ct
			);

			if (page.Items.Count == 0)
				break;

			logger.ZLogWarning(message: $"Found {page.Items.Count} new unresolvable event(s) requiring manual intervention.");

			foreach (UnresolvableEvent @event in page.Items)
			{
				logger.ZLogWarning(message: $"""
					Unresolvable event: Id={@event.Id}, Type={@event.Type}, ReferenceId={@event.ReferenceId},
					Reason={@event.Reason}, OccurredAt={@event.OccurredAt:O}.
				""");
			}

			await unresolvableEventWriteRepository.AcknowledgeBatchAsync(
				ids: page.Items.Select(selector: e => e.Id).ToList(),
				acknowledgedAt: dateProvider.UtcNow,
				ct: ct
			);

			totalLogged += page.Items.Count;
		} while (page.HasNextPage);

		int stillUnresolved = await unresolvableEventReadRepository.CountUnresolvedAsync(ct: ct);
		WorkerMetrics.UnresolvableEventsPending.Record(value: stillUnresolved);

		if (totalLogged > 0)
		{
			logger.ZLogWarning(message: $"""
				Dead letter scan complete. New unresolvable events acknowledged: {totalLogged}.
				Total still awaiting resolution: {stillUnresolved}.
				See DeadLetterBacklogSummaryJob for a periodic reminder of anything still unresolved.
			""");
		}
	}
}
