using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.Snapshot;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.Cleanup.Jobs;

[DisallowConcurrentExecution]
public sealed class CleanupJob(
	IIdempotencyWriteRepository idempotencyRepository,
	IOutboxWriteRepository outboxRepository,
	IProcessedMessageWriteRepository processedMessageRepository,
	ISnapshotWriteRepository snapshotRepository,
	IDateProvider dateProvider,
	IOptions<CleanupOptions> options,
	ILogger<CleanupJob> logger
) : IJob
{
	private readonly CleanupOptions _options = options.Value;

	public async Task Execute(IJobExecutionContext context)
		=> await RunAsync(ct: context.CancellationToken);

	private async Task RunAsync(CancellationToken ct)
	{
		DateTime now = dateProvider.UtcNow;

		await CleanupIdempotentCommandsAsync(now: now, ct: ct);
		await CleanupProcessedMessagesAsync(now: now, ct: ct);
		await CleanupOutboxProcessedAsync(now: now, ct: ct);
		await CleanupOutboxFailedAsync(now: now, ct: ct);
		await CleanupSnapshotsAsync(ct: ct);
	}

	private async Task CleanupIdempotentCommandsAsync(DateTime now, CancellationToken ct)
	{
		int total = await DeleteInBatchesAsync(
			deleteFunc: batchSize => idempotencyRepository.DeleteExpiredAsync(before: now, batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] idempotent_commands: deleted {total} expired row(s).");
	}

	private async Task CleanupProcessedMessagesAsync(DateTime now, CancellationToken ct)
	{
		DateTime before = now.AddDays(value: -_options.ProcessedMessageRetentionDays);

		int total = await DeleteInBatchesAsync(
			deleteFunc: batchSize => processedMessageRepository.DeleteOldAsync(before: before, batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] processed_messages: deleted {total} row(s) older than {_options.ProcessedMessageRetentionDays} day(s).");
	}

	private async Task CleanupOutboxProcessedAsync(DateTime now, CancellationToken ct)
	{
		DateTime before = now.AddDays(value: -_options.OutboxProcessedRetentionDays);

		int total = await DeleteInBatchesAsync(
			deleteFunc: batchSize => outboxRepository.DeleteProcessedAsync(before: before, batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] outbox_messages (processed): deleted {total} row(s) older than {_options.OutboxProcessedRetentionDays} day(s).");
	}

	private async Task CleanupOutboxFailedAsync(DateTime now, CancellationToken ct)
	{
		DateTime before = now.AddDays(value: -_options.OutboxFailedRetentionDays);

		int total = await DeleteInBatchesAsync(
			deleteFunc: batchSize => outboxRepository.DeleteFailedAsync(before: before, batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] outbox_messages (failed): deleted {total} row(s) older than {_options.OutboxFailedRetentionDays} day(s).");
	}

	private async Task CleanupSnapshotsAsync(CancellationToken ct)
	{
		int total = await DeleteInBatchesAsync(
			deleteFunc: batchSize => snapshotRepository.DeleteOldAsync(batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] snapshots: deleted {total} non-latest row(s).");
	}

	private async Task<int> DeleteInBatchesAsync(
		Func<int, Task<int>> deleteFunc,
		CancellationToken ct)
	{
		int total = 0;
		int deleted;

		do
		{
			if (ct.IsCancellationRequested)
				break;

			deleted = await deleteFunc(_options.BatchSize);
			total += deleted;
		}
		while (deleted == _options.BatchSize);

		return total;
	}
}