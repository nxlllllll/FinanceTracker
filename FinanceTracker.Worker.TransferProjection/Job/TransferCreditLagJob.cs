using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.TransferCompensation;
using FinanceTracker.Worker.Shared.Job;
using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.TransferProjection.Job;

[DisallowConcurrentExecution]
public sealed class TransferCreditLagJob(
	ITransferReadRepository transferReadRepository,
	ITransferCompensationService compensationService,
	IOptionsMonitor<TransferCreditLagOptions> options,
	ILogger<TransferCreditLagJob> logger
) : BaseJob<TransferCreditLagOptions>(options: options, logger: logger)
{
	protected override async Task ProcessAsync(TransferCreditLagOptions options, CancellationToken ct)
	{
		await MonitorLagAsync(options: options, ct: ct);
		await CompensateStuckTransfersAsync(options: options, ct: ct);
	}

	private async Task MonitorLagAsync(TransferCreditLagOptions options, CancellationToken ct)
	{
		TimeSpan gracePeriod = TimeSpan.FromMinutes(value: options.GracePeriodMinutes);

		int pendingCount = await transferReadRepository.GetPendingCreditCountAsync(
			gracePeriod: gracePeriod,
			ct: ct
		);

		WorkerMetrics.TransferCreditPending.Record(value: pendingCount);

		if (pendingCount > 0)
			logger.ZLogWarning(message: $"[{nameof(TransferCreditLagJob)}] {pendingCount} transfer(s) pending credit after {gracePeriod.TotalMinutes} min grace period.");
	}

	private async Task CompensateStuckTransfersAsync(TransferCreditLagOptions options, CancellationToken ct)
	{
		TimeSpan compensationThreshold = TimeSpan.FromMinutes(value: options.CompensationThresholdMinutes);

		IReadOnlyList<Core.ReadModels.PendingCreditTransfer> stuck = await transferReadRepository.GetPendingCreditForCompensationAsync(
			compensationThreshold: compensationThreshold, 
			ct: ct
		);

		if (stuck.Count == 0)
			return;

		logger.ZLogWarning(message: $"""
			[{nameof(TransferCreditLagJob)}] {stuck.Count} transfer(s) exceeded compensation threshold of {compensationThreshold.TotalMinutes} min. Compensating.
		""");

		foreach (Core.ReadModels.PendingCreditTransfer transfer in stuck)
		{
			if (ct.IsCancellationRequested)
				break;

			await compensationService.CompensateAsync(transfer: transfer, ct: ct);
		}
	}
}