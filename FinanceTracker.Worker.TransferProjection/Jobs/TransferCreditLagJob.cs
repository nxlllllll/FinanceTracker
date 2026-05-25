using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.TransferProjection.Jobs;

[DisallowConcurrentExecution]
public sealed class TransferCreditLagJob(
	ITransferReadRepository transferReadRepository,
	IOptionsMonitor<TransferCreditLagOptions> options,
	ILogger<TransferCreditLagJob> logger
) : IJob
{
	public async Task Execute(IJobExecutionContext executionContext)
	{
		if (!options.CurrentValue.IsEnabled)
		{
			logger.ZLogInformation(message: $"[{nameof(TransferCreditLagJob)}] Disabled. Skipping.");
			return;
		}

		TimeSpan gracePeriod = TimeSpan.FromMinutes(value: options.CurrentValue.GracePeriodMinutes);

		int pendingCount = await transferReadRepository.GetPendingCreditCountAsync(
			gracePeriod: gracePeriod,
			ct: executionContext.CancellationToken
		);

		WorkerMetrics.TransferCreditPending.Record(value: pendingCount);

		if (pendingCount > 0)
			logger.ZLogWarning(message: $"[{nameof(TransferCreditLagJob)}] {pendingCount} transfer(s) have debit applied but no credit recorded after {gracePeriod.TotalMinutes} min grace period.");
	}
}
