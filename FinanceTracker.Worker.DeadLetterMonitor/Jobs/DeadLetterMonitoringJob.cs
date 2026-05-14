using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.EntityFrameworkCore;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.DeadLetterMonitor.Jobs;

[DisallowConcurrentExecution]
public sealed class DeadLetterMonitoringJob(
	FinanceTrackerContext context,
	ILogger<DeadLetterMonitoringJob> logger
) : IJob
{
	public async Task Execute(IJobExecutionContext executionContext)
	{
		List<OutboxMessageEntity> deadLetters = await context.OutboxMessages.AsNoTracking()
			.Where(predicate: m => m.FailedAt != null)
			.OrderBy(keySelector: m => m.FailedAt)
			.ToListAsync(cancellationToken: executionContext.CancellationToken);
 
		WorkerMetrics.DeadLetterCount.Record(value: deadLetters.Count);
		if (deadLetters.Count == 0)
			return;
 
		logger.ZLogWarning(message: $"Found {deadLetters.Count} dead-letter outbox message(s).");
 
		foreach (OutboxMessageEntity message in deadLetters)
			logger.ZLogWarning(message: $"Dead-letter outbox message: {message}");
	}
}