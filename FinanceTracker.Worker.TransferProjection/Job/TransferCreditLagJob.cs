using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.TransferProjection.Job;

[DisallowConcurrentExecution]
public sealed class TransferCreditLagJob(
	ITransferReadRepository transferReadRepository,
	IOptionsMonitor<TransferCreditLagOptions> options,
	ILogger<TransferCreditLagJob> logger
) : BaseJob<TransferCreditLagOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(TransferCreditLagOptions options, CancellationToken ct)
	{
		TimeSpan gracePeriod = TimeSpan.FromMinutes(value: options.GracePeriodMinutes);

		int pendingCount = await transferReadRepository.GetPendingCreditCountAsync(
			gracePeriod: gracePeriod,
			ct: ct
		);

		WorkerMetrics.TransferCreditPending.Record(value: pendingCount);

		if (pendingCount > 0)
			logger.ZLogWarning(message: $"[{nameof(TransferCreditLagJob)}] {pendingCount} transfer(s) have debit applied but no credit recorded after {gracePeriod.TotalMinutes} min grace period.");
	}
}