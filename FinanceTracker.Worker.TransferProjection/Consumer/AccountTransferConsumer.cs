using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Contracts.Events.Account;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.TransferCompensation;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Worker.Shared.Metrics;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using ZLogger;

namespace FinanceTracker.Worker.TransferProjection.Consumer;

/// <summary>
/// Applies the credit side of a transfer by consuming <see cref="TransferDebitedEvent"/>.
/// </summary>
/// <remarks>
/// Transfer credit is intentionally eventual: debit and credit happen in separate transactions
/// across separate workers. The debit is applied synchronously in the command handler;
/// this consumer applies the credit asynchronously via the outbox > RabbitMQ pipeline.
///
/// <b>Failure handling:</b>
/// <list type="bullet">
///   <item>If <c>toAccount</c> is not found — <see cref="ITransferCompensationService"/> refunds <c>fromAccount</c>.</item>
///   <item>If both accounts are missing — the event is escalated to <c>unresolvable_events</c> for manual resolution.</item>
///   <item>If an event this consumer owns cannot be deserialized — the exception propagates so the
///         listener rejects the delivery and RabbitMQ eventually routes it to the dead letter queue.
///         Swallowing it would ack the message, destroying it silently.</item>
///   <item>If this consumer is stuck or dead — <c>transfer.credit.pending</c> metric will rise above 0
///         after the configured grace period, triggering an alert.</item>
/// </list>
/// </remarks>
[RoutingKey(routingKey: AggregateTypeNames.Account)]
public sealed class AccountTransferConsumer(
	IAccountRepository accountRepository,
	ITransferRepository transferRepository,
	ITransferWriteRepository transferWriteRepository,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	IProcessedMessageReadRepository processedMessageReadRepository,
	IProcessedMessageWriteRepository processedMessageWriteRepository,
	ITransferCompensationService compensationService,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	ILogger<AccountTransferConsumer> logger
) : IMessageHandler<AggregateEventsMessage>
{
	public async Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		IReadOnlyList<AccountTransferDebitedEvent> debitEvents = ExtractDebitEvents(message: message);
		if (debitEvents.Count == 0)
			return;

		using IDisposable? scope = logger.BeginScope(state: new Dictionary<string, object> { ["CorrelationId"] = message.CorrelationId });

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			if (await processedMessageReadRepository.IsProcessedAsync(messageId: message.MessageId, consumerType: nameof(AccountTransferConsumer), ct: ct))
			{
				logger.ZLogWarning(message: $"[{message.CorrelationId}] Message {message.MessageId} already processed.");
				return;
			}

			foreach (AccountTransferDebitedEvent debitEvent in debitEvents)
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
		Transfer? transfer = await transferRepository.GetByIdAsync(transferId: debitEvent.TransferId, ct: ct);
		if (transfer is null)
		{
			logger.ZLogError(message: $"[{correlationId}] Transfer {debitEvent.TransferId} not found. Skipping.");
			return;
		}

		if (transfer.Status != TransferStatus.PendingCredit)
		{
			logger.ZLogWarning(message: $"[{correlationId}] Transfer {debitEvent.TransferId} is in {transfer.Status} state, expected PendingCredit. Skipping (already processed).");
			return;
		}

		Account? toAccount = await accountRepository.GetByIdAsync(accountId: debitEvent.ToAccountId, ct: ct);
		if (toAccount is null)
		{
			logger.ZLogError(message: $"[{correlationId}] toAccount {debitEvent.ToAccountId} not found. Compensating transfer {debitEvent.TransferId}.");
			await compensationService.CompensateAsync(transfer: new PendingCreditTransfer(
				TransferId: debitEvent.TransferId,
				FromAccountId: debitEvent.AccountId,
				Amount: debitEvent.Amount,
				OccurredAt: debitEvent.OccurredAt
			), ct: ct);
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
			await compensationService.CompensateAsync(transfer: new PendingCreditTransfer(
				TransferId: debitEvent.TransferId,
				FromAccountId: debitEvent.AccountId,
				Amount: debitEvent.Amount,
				OccurredAt: debitEvent.OccurredAt
			), ct: ct);
			return;
		}

		Result<Unit, DomainException> completeResult = transfer.Complete();
		if (completeResult.IsFailure)
		{
			logger.ZLogWarning(message: $"[{correlationId}] Transfer {debitEvent.TransferId} cannot be completed: {completeResult.Error!.Message}.");
			return;
		}

		await accountRepository.SaveAsync(account: toAccount, ct: ct);
		await transferWriteRepository.SaveStatusAsync(transfer: transfer, ct: ct);

		WorkerMetrics.TransfersCompleted.Add(delta: 1);

		double durationMs = (dateProvider.UtcNow - debitEvent.OccurredAt).TotalMilliseconds;
		WorkerMetrics.TransferCreditDuration.Record(value: durationMs);

		logger.ZLogInformation(message: $"[{correlationId}] Transfer {debitEvent.TransferId} completed: {debitEvent.AccountId} > {debitEvent.ToAccountId}.");
	}

	private IReadOnlyList<AccountTransferDebitedEvent> ExtractDebitEvents(AggregateEventsMessage message)
	{
		List<AccountTransferDebitedEvent> debitEvents = [];

		foreach (EventEnvelope envelope in message.Events)
		{
			Type type;
			try
			{
				type = integrationEventTypeResolver.ResolveType(eventType: envelope.EventType);
			}
			catch (InvalidOperationException)
			{
				logger.ZLogDebug(message: $"Skipping unrecognised event type '{envelope.EventType}'.");
				continue;
			}

			if (type != typeof(AccountTransferDebitedEvent))
				continue;

			AccountTransferDebitedEvent debitEvent = (AccountTransferDebitedEvent?)JsonSerializer.Deserialize(
				json: envelope.EventPayload,
				returnType: type,
				options: FinanceTrackerJsonOptions.Payload
			) ?? throw new SerializationException(message: $"Payload of '{envelope.EventType}' deserialized to null.");

			debitEvents.Add(item: debitEvent);
		}

		return debitEvents;
	}
}
