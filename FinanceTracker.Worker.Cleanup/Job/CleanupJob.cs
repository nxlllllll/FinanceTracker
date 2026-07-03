using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.Snapshot;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.Job;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.Cleanup.Job;

[DisallowConcurrentExecution]
public sealed class CleanupJob(
	IIdempotencyWriteRepository idempotencyRepository,
	IOutboxWriteRepository outboxRepository,
	IProcessedMessageWriteRepository processedMessageRepository,
	ISnapshotWriteRepository snapshotRepository,
	IDateProvider dateProvider,
	IOptionsMonitor<CleanupOptions> options,
	ILogger<CleanupJob> logger
) : BaseJob<CleanupOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(CleanupOptions options, CancellationToken ct)
	{
		DateTimeOffset now = dateProvider.UtcNow;

		await CleanupIdempotentCommandsAsync(options: options, now: now, ct: ct);
		await CleanupProcessedMessagesAsync(options: options, now: now, ct: ct);
		await CleanupOutboxProcessedAsync(options: options, now: now, ct: ct);
		await CleanupOutboxFailedAsync(options: options, now: now, ct: ct);
		await CleanupSnapshotsAsync(options: options, ct: ct);
	}

	private async Task CleanupIdempotentCommandsAsync(CleanupOptions options, DateTimeOffset now, CancellationToken ct)
	{
		int total = await DeleteInBatchesAsync(
			batchSize: options.BatchSize,
			deleteFunc: batchSize => idempotencyRepository.DeleteExpiredAsync(before: now, batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] idempotent_commands: deleted {total} expired row(s).");
	}

	private async Task CleanupProcessedMessagesAsync(CleanupOptions options, DateTimeOffset now, CancellationToken ct)
	{
		DateTimeOffset before = now.AddDays(days: -options.ProcessedMessageRetentionDays);

		int total = await DeleteInBatchesAsync(
			batchSize: options.BatchSize,
			deleteFunc: batchSize => processedMessageRepository.DeleteOldAsync(before: before, batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] processed_messages: deleted {total} row(s) older than {options.ProcessedMessageRetentionDays} day(s).");
	}

	private async Task CleanupOutboxProcessedAsync(CleanupOptions options, DateTimeOffset now, CancellationToken ct)
	{
		DateTimeOffset before = now.AddDays(days: -options.OutboxProcessedRetentionDays);

		int total = await DeleteInBatchesAsync(
			batchSize: options.BatchSize,
			deleteFunc: batchSize => outboxRepository.DeleteProcessedAsync(before: before, batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] outbox_messages (processed): deleted {total} row(s) older than {options.OutboxProcessedRetentionDays} day(s).");
	}

	private async Task CleanupOutboxFailedAsync(CleanupOptions options, DateTimeOffset now, CancellationToken ct)
	{
		DateTimeOffset before = now.AddDays(days: -options.OutboxFailedRetentionDays);

		int total = await DeleteInBatchesAsync(
			batchSize: options.BatchSize,
			deleteFunc: batchSize => outboxRepository.DeleteFailedAsync(before: before, batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] outbox_messages (failed): deleted {total} row(s) older than {options.OutboxFailedRetentionDays} day(s).");
	}

	private async Task CleanupSnapshotsAsync(CleanupOptions options, CancellationToken ct)
	{
		int total = await DeleteInBatchesAsync(
			batchSize: options.BatchSize,
			deleteFunc: batchSize => snapshotRepository.DeleteOldAsync(batchSize: batchSize, ct: ct),
			ct: ct
		);

		if (total > 0)
			logger.ZLogInformation(message: $"[Cleanup] snapshots: deleted {total} non-latest row(s).");
	}

	private async Task<int> DeleteInBatchesAsync(
		int batchSize,
		Func<int, Task<int>> deleteFunc,
		CancellationToken ct)
	{
		int total = 0;
		int deleted;

		do
		{
			if (ct.IsCancellationRequested)
				break;

			deleted = await deleteFunc(batchSize);
			total += deleted;
		}
		while (deleted == batchSize);

		return total;
	}
}
