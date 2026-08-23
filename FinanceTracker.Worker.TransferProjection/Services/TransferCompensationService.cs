using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.TransferCompensation;
using FinanceTracker.Worker.Shared.Metrics;
using FinanceTracker.Worker.TransferProjection.Job;
using ZLogger;

namespace FinanceTracker.Worker.TransferProjection.Services;

/// <summary>
/// Compensates a stuck transfer by refunding the debit to the source account.
/// Mirrors the compensation logic in <see cref="Consumer.AccountTransferConsumer"/>
/// but operates on transfers identified by <see cref="TransferCreditLagJob"/>
/// rather than on RabbitMQ message failures.
/// </summary>
public sealed class TransferCompensationService(
	IAccountRepository accountRepository,
	ITransferRepository transferRepository,
	ITransferWriteRepository transferWriteRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IDateProvider dateProvider,
	ILogger<TransferCompensationService> logger
) : ITransferCompensationService
{
	public async Task CompensateAsync(PendingCreditTransfer pendingTransfer, CancellationToken ct = default)
	{
		Transfer? transfer = await transferRepository.GetByIdAsync(transferId: pendingTransfer.TransferId, ct: ct);

		if (transfer is null)
		{
			logger.ZLogError(message: $"[Compensation] Transfer {pendingTransfer.TransferId} not found. Skipping.");
			return;
		}

		if (transfer.Status != TransferStatus.PendingCredit)
		{
			logger.ZLogInformation(message: $"[Compensation] Transfer {pendingTransfer.TransferId} is already in {transfer.Status} state. Skipping (already processed).");
			return;
		}

		Account? fromAccount = await accountRepository.GetByIdAsync(
			accountId: pendingTransfer.FromAccountId,
			ct: ct
		);

		if (fromAccount is null)
		{
			logger.ZLogError(message: $"[Compensation] fromAccount {pendingTransfer.FromAccountId} not found for transfer {pendingTransfer.TransferId}. Escalating to unresolvable.");
			await EscalateToUnresolvableAsync(transfer: transfer, pendingTransfer: pendingTransfer, reason: "fromAccount not found during lag compensation.", ct: ct);
			return;
		}

		Result<Unit, DomainException> refundResult = fromAccount.RefundTransfer(
			occurredAt: dateProvider.UtcNow,
			transferId: pendingTransfer.TransferId,
			amount: pendingTransfer.Amount,
			description: "Refund: credit side not received within compensation threshold."
		);

		if (refundResult.IsFailure)
		{
			logger.ZLogError(message: $"[Compensation] RefundTransfer failed for transfer {pendingTransfer.TransferId}: {refundResult.Error!.Message}. Escalating to unresolvable.");
			await EscalateToUnresolvableAsync(transfer: transfer, pendingTransfer: pendingTransfer, reason: refundResult.Error!.Message, ct: ct);
			return;
		}

		Result<Unit, DomainException> compensateResult = transfer.Compensate(occurredAt: dateProvider.UtcNow);
		if (compensateResult.IsFailure)
		{
			logger.ZLogWarning(message: $"[Compensation] Transfer {pendingTransfer.TransferId} cannot be compensated: {compensateResult.Error!.Message}. Skipping.");
			return;
		}

		await accountRepository.SaveAsync(account: fromAccount, ct: ct);
		await transferWriteRepository.SaveStatusAsync(transfer: transfer, ct: ct);

		WorkerMetrics.TransfersCompensated.Add(delta: 1);

		logger.ZLogWarning(message: $"[Compensation] Transfer {pendingTransfer.TransferId} compensated via lag job: refunded {pendingTransfer.Amount} to account {pendingTransfer.FromAccountId} (rate lifecycle: {transfer.RateStatus}).");
	}

	private async Task EscalateToUnresolvableAsync(
		Transfer transfer,
		PendingCreditTransfer pendingTransfer,
		string reason,
		CancellationToken ct)
	{
		Result<Unit, DomainException> failResult = transfer.Fail(occurredAt: dateProvider.UtcNow);
		if (failResult.IsFailure)
		{
			logger.ZLogWarning(message: $"[Compensation] Transfer {pendingTransfer.TransferId} cannot be failed: {failResult.Error!.Message}.");
			return;
		}

		await unresolvableEventWriteRepository.CreateAsync(
			type: UnresolvableEventType.TransferCompensation,
			referenceId: pendingTransfer.TransferId,
			reason: reason,
			payload: JsonSerializer.Serialize(value: new { pendingTransfer.FromAccountId, pendingTransfer.Amount }),
			occurredAt: dateProvider.UtcNow,
			ct: ct
		);

		await transferWriteRepository.SaveStatusAsync(transfer: transfer, ct: ct);

		WorkerMetrics.TransfersFailed.Add(delta: 1);

		logger.ZLogError(message: $"[Compensation] Transfer {pendingTransfer.TransferId} escalated to unresolvable_events. Reason: {reason}.");
	}
}
