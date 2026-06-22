using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.Job;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.DeadLetterMonitor.Job;

/// <summary>
/// Runs infrequently (daily by default) to remind operators of anything still unresolved, regardless
/// of whether <see cref="DeadLetterMonitoringJob"/> already reported it once. Without this, a backlog
/// item that was acknowledged the moment it appeared — but never actually fixed — would fall out of
/// view entirely after its one-time alert.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DeadLetterBacklogSummaryJob(
	IUnresolvableEventReadRepository unresolvableEventReadRepository,
	IDateProvider dateProvider,
	IOptionsMonitor<DeadLetterBacklogSummaryOptions> options,
	ILogger<DeadLetterBacklogSummaryJob> logger
) : BaseJob<DeadLetterBacklogSummaryOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(DeadLetterBacklogSummaryOptions options, CancellationToken ct)
	{
		DateTimeOffset cutoff = dateProvider.UtcNow.AddHours(hours: -options.UnresolvedOlderThanHours);

		UnresolvedBacklogSummary summary = await unresolvableEventReadRepository.GetUnresolvedOlderThanAsync(
			cutoff: cutoff,
			sampleSize: options.SampleSize,
			ct: ct
		);

		if (summary.TotalCount == 0)
		{
			logger.ZLogInformation(message: $"No unresolved events older than {options.UnresolvedOlderThanHours}h.");
			return;
		}

		logger.ZLogWarning(message: $"""
			Unresolved backlog: {summary.TotalCount} event(s) older than {options.UnresolvedOlderThanHours}h,
			oldest from {summary.OldestOccurredAt:O}. None have been marked resolved since first flagged.
		""");

		foreach (UnresolvableEvent @event in summary.Sample)
		{
			logger.ZLogWarning(message: $"""
				Still unresolved: Id={@event.Id}, Type={@event.Type}, ReferenceId={@event.ReferenceId},
				Reason={@event.Reason}, OccurredAt={@event.OccurredAt:O}.
			""");
		}
	}
}