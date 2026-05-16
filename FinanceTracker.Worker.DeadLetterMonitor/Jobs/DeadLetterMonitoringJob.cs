using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Worker.Shared.Metrics;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.DeadLetterMonitor.Jobs;

[DisallowConcurrentExecution]
public sealed class DeadLetterMonitoringJob(
	IOutboxReadRepository outboxReadRepository,
	ILogger<DeadLetterMonitoringJob> logger
) : IJob
{
	public async Task Execute(IJobExecutionContext executionContext)
	{
		IReadOnlyList<DeadLetterMessage> deadLetters = await outboxReadRepository.GetDeadLettersAsync(ct: executionContext.CancellationToken);

		WorkerMetrics.DeadLetterCount.Record(value: deadLetters.Count);
		if (deadLetters.Count == 0)
			return;

		logger.ZLogWarning(message: $"Found {deadLetters.Count} dead-letter outbox message(s).");

		foreach (DeadLetterMessage message in deadLetters)
			logger.ZLogWarning(message: $"Dead-letter: Id={message.Id}, AggregateId={message.AggregateId}, RetryCount={message.RetryCount}, FailedAt={message.FailedAt}.");
	}
}