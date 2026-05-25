using System.Text.Json;
using FinanceTracker.Contracts.Events.Account;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Worker.Shared.Metrics;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using ZLogger;

namespace FinanceTracker.Worker.TransferProjection.Consumers;

/// <summary>
/// Applies the credit side of a transfer by consuming <see cref="AccountTransferDebitedEvent"/>.
/// </summary>
/// <remarks>
/// Transfer credit is intentionally eventual: debit and credit happen in separate transactions
/// across separate workers. The debit is applied synchronously in the command handler;
/// this consumer applies the credit asynchronously via the outbox → RabbitMQ pipeline.
///
/// <b>Failure handling:</b>
/// <list type="bullet">
///   <item>If <c>toAccount</c> is not found — <see cref="CompensateAsync"/> refunds <c>fromAccount</c>.</item>
///   <item>If both accounts are missing — the event is escalated to <c>unresolvable_events</c> for manual resolution.</item>
///   <item>If this consumer is stuck or dead — <c>transfer.credit.pending</c> metric will rise above 0
///         after the configured grace period, triggering an alert.</item>
/// </list>
/// </remarks>
public sealed class AccountTransferConsumer(
	IAccountRepository accountRepository,
    IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	IProcessedMessageReadRepository processedMessageReadRepository,
	IProcessedMessageWriteRepository processedMessageWriteRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	ILogger<AccountTransferConsumer> logger
) : IMessageHandler<AggregateEventsMessage>
{
	public async Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{

		AccountTransferDebitedEvent? debitEvent = ExtractDebitEvent(message: message);
		if (debitEvent is null)
			return;

		using IDisposable? scope = logger.BeginScope(state: new Dictionary<string, object> { ["CorrelationId"] = message.CorrelationId });

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			if (await processedMessageReadRepository.IsProcessedAsync(messageId: message.MessageId, consumerType: nameof(AccountTransferConsumer), ct: ct))
			{
				logger.ZLogWarning(message: $"[{message.CorrelationId}] Message {message.MessageId} already processed.");
				return;
			}

			await ExecuteCreditAsync(debitEvent: debitEvent, correlationId: message.CorrelationId, ct: ct);

			await processedMessageWriteRepository.MarkAsProcessedAsync(
				messageId: message.MessageId,
				consumerType: nameof(AccountTransferConsumer),
				processedAt: dateProvider.UtcNow,
				ct: ct
			);
		}, ct: ct);
	}

	private async Task ExecuteCreditAsync(
		AccountTransferDebitedEvent debitEvent,
		Guid correlationId,
		CancellationToken ct)
	{
		Account? toAccount = await accountRepository.GetByIdAsync(accountId: debitEvent.ToAccountId, ct: ct);

		if (toAccount is null)
		{
			logger.ZLogError(message: $"[{correlationId}] toAccount {debitEvent.ToAccountId} not found. Compensating transfer {debitEvent.TransferId}.");
			await CompensateAsync(debitEvent: debitEvent, correlationId: correlationId, reason: "ToAccount not found.", ct: ct);
			return;
		}

		Result<Unit, DomainException> creditResult = toAccount.CreditTransfer(
			occurredAt: dateProvider.UtcNow,
			transferId: debitEvent.TransferId,
			fromAccountId: debitEvent.AccountId,
			amount: debitEvent.Amount,
			exchangeRate: debitEvent.ForexRate,
			description: debitEvent.Description
		);

		if (creditResult.IsFailure)
		{
			logger.ZLogError(message: $"[{correlationId}] CreditTransfer failed: {creditResult.Error?.Message}. Compensating transfer {debitEvent.TransferId}.");
			await CompensateAsync(debitEvent: debitEvent, correlationId: correlationId, reason: creditResult.Error?.Message, ct: ct);
			return;
		}

		await accountRepository.SaveAsync(account: toAccount, ct: ct);

		double durationMs = (dateProvider.UtcNow - debitEvent.OccurredAt).TotalMilliseconds;
		WorkerMetrics.TransferCreditDuration.Record(value: durationMs);

		logger.ZLogInformation(message: $"[{correlationId}] Transfer {debitEvent.TransferId} completed: {debitEvent.AccountId} → {debitEvent.ToAccountId}.");
	}

	private async Task CompensateAsync(
		AccountTransferDebitedEvent debitEvent,
		Guid correlationId,
		string? reason,
		CancellationToken ct)
	{
		Account? fromAccount = await accountRepository.GetByIdAsync(accountId: debitEvent.AccountId, ct: ct);

		if (fromAccount is null)
		{
			logger.ZLogError(message: $"[{correlationId}] Compensation FAILED: fromAccount {debitEvent.AccountId} not found. Transfer {debitEvent.TransferId} requires manual resolution.");
			
			string payload = JsonSerializer.Serialize(value: new
			{
				fromAccountId = debitEvent.AccountId,
				amount = debitEvent.Amount,
				correlationId = correlationId
			});

			await unresolvableEventWriteRepository.CreateAsync(
				type: UnresolvableEventType.TransferCompensation,
				referenceId: debitEvent.TransferId,
				reason: reason ?? "fromAccount not found.",
				payload: payload,
				occurredAt: dateProvider.UtcNow,
				ct: ct
			);

			return;
		}

		Result<Unit, DomainException> refundResult = fromAccount.RefundTransfer(
			occurredAt: dateProvider.UtcNow, 
			transferId: debitEvent.TransferId, 
			amount: debitEvent.Amount,
			description: $"Refund: {reason}"
		);

		if (refundResult.IsFailure)
		{
			logger.ZLogError(message: $"[{correlationId}] Refund failed for transfer {debitEvent.TransferId}: {refundResult.Error!.Message}. Manual resolution required.");
			await unresolvableEventWriteRepository.CreateAsync(
				type: UnresolvableEventType.TransferCompensation,
				referenceId: debitEvent.TransferId,
				reason: refundResult.Error!.Message,
				payload: JsonSerializer.Serialize(value: new { FromAccountId = debitEvent.AccountId }),
				occurredAt: dateProvider.UtcNow,
				ct: ct
			);
			return;
		}
		
		await accountRepository.SaveAsync(account: fromAccount, ct: ct);

		logger.ZLogWarning(message: $"[{correlationId}] Compensation executed: refunded {debitEvent.Amount} to {debitEvent.AccountId} for transfer {debitEvent.TransferId}.");
	}

	private AccountTransferDebitedEvent? ExtractDebitEvent(AggregateEventsMessage message)
	{
		foreach (EventEnvelope envelope in message.Events)
		{
			try
			{
				Type type = integrationEventTypeResolver.ResolveType(eventType: envelope.EventType);
				if (type != typeof(AccountTransferDebitedEvent))
					continue;

				return (AccountTransferDebitedEvent)JsonSerializer.Deserialize(
					json: envelope.EventPayload,
					returnType: type,
					options: FinanceTrackerJsonOptions.Payload
				)!;
			}
			catch (Exception exception)
			{
				logger.ZLogWarning(exception: exception, message: $"Failed to deserialize envelope with event type '{envelope.EventType}'.");
			}
		}

		return null;
	}
}