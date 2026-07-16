using System.Text.Json;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Worker.Shared.Metrics;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using ZLogger;

namespace FinanceTracker.Worker.RecurringTransactionProjection.Consumer;

/// <summary>
/// RabbitMQ message handler that receives <see cref="RecurringTransactionTriggeredMessage"/>
/// from the recurring transaction exchange, deduplicates via <c>processed_messages</c>,
/// and creates the actual transaction through <c>TransactionCreationService</c>.
/// Any failure to produce the transaction — a data-integrity problem (missing recurring
/// transaction/account, invalid message data) or a domain rule rejection (e.g. insufficient
/// funds) — is escalated to <c>unresolvable_events</c> rather than silently skipped. The
/// message is still marked processed in every case: none of these causes are retryable, so
/// retrying would just repeat the same outcome.
/// </summary>
public sealed class RecurringTransactionConsumer(
	IAccountRepository accountRepository,
	ITransactionCreationService transactionCreationService,
	IRecurringTransactionReadRepository recurringTransactionReadRepository,
	IProcessedMessageReadRepository processedMessageReadRepository,
	IProcessedMessageWriteRepository processedMessageWriteRepository,
	IUnresolvableEventWriteRepository unresolvableEventWriteRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	ILogger<RecurringTransactionConsumer> logger
) : IMessageHandler<RecurringTransactionTriggeredMessage>
{
	public async Task HandleAsync(
		RecurringTransactionTriggeredMessage message,
		CancellationToken ct = default)
	{
		using IDisposable? scope = logger.BeginScope(state: new Dictionary<string, object> { ["CorrelationId"] = message.CorrelationId });

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			if (await processedMessageReadRepository.IsProcessedAsync(messageId: message.MessageId, consumerType: nameof(RecurringTransactionConsumer), ct: ct))
			{
				logger.ZLogWarning(message: $"[{message.CorrelationId}] Message {message.MessageId} already processed.");
				return;
			}

			RecurringTransactionReadModel? recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(
				recurringTransactionId: message.RecurringTransactionId,
				userId: message.UserId,
				ct: ct
			);

			if (recurringTransaction is null)
			{
				await EscalateToUnresolvableAsync(message: message, reason: $"Recurring transaction {message.RecurringTransactionId} not found.", ct: ct);
				return;
			}

			Account? account = await accountRepository.GetByIdAsync(accountId: message.AccountId, ct: ct);
			if (account is null)
			{
				await EscalateToUnresolvableAsync(message: message, reason: $"Account {message.AccountId} not found.", ct: ct);
				return;
			}

			Result<Currency, DomainException> currencyResult = Currency.Create(value: message.Currency);
			if (currencyResult.IsFailure)
			{
				await EscalateToUnresolvableAsync(message: message, reason: $"Invalid currency '{message.Currency}' in message {message.MessageId}.", ct: ct);
				return;
			}

			if (!Enum.TryParse(value: message.Direction, ignoreCase: true, result: out DirectionType direction))
			{
				await EscalateToUnresolvableAsync(message: message, reason: $"Invalid direction '{message.Direction}' in message {message.MessageId}.", ct: ct);
				return;
			}

			Result<Transaction, DomainException> result = await transactionCreationService.CreateAsync(command: new CreateTransactionCommand(
				AccountId: message.AccountId,
				UserId: message.UserId,
				CategoryId: message.CategoryId,
				Amount: message.Amount,
				Currency: currencyResult.Value,
				Direction: direction,
				Description: message.Description,
				OccurredAt: message.OccurredAt
			), account: account, ct: ct);

			if (result.IsFailure)
			{
				await EscalateToUnresolvableAsync(message: message, reason: result.Error!.Message, ct: ct);
				return;
			}

			Transaction transaction = result.Value!;

			WorkerMetrics.TransactionsCreated.Add(delta: 1, new KeyValuePair<string, object?>(key: "direction", value: message.Direction));

			await MarkProcessedAsync(message: message, ct: ct);

			logger.ZLogInformation(message: $"""
				[Audit] Transaction created. TransactionId: {transaction.Id}, UserId: {transaction.UserId}, AccountId: {transaction.AccountId},
				CategoryId: {transaction.CategoryId}, Amount: {transaction.Amount}, Direction: {transaction.Direction},
				ExchangeRate: {transaction.ExchangeRate}, RateStatus: {transaction.RateStatus}, OccurredAt: {transaction.OccurredAt:O}.
			""");

			logger.ZLogInformation(message: $"[{message.CorrelationId}] Created transaction for recurring transaction {message.RecurringTransactionId}.");
		}, ct: ct);
	}

	private async Task EscalateToUnresolvableAsync(
		RecurringTransactionTriggeredMessage message,
		string reason,
		CancellationToken ct)
	{
		await unresolvableEventWriteRepository.CreateAsync(
			type: UnresolvableEventType.RecurringTransactionFailed,
			referenceId: message.RecurringTransactionId,
			reason: reason,
			payload: JsonSerializer.Serialize(value: new
			{
				message.MessageId,
				message.AccountId,
				message.CategoryId,
				message.Amount,
				message.Currency,
				message.Direction,
				message.OccurredAt
			}),
			occurredAt: dateProvider.UtcNow,
			ct: ct
		);

		WorkerMetrics.RecurringTransactionsFailed.Add(delta: 1);

		await MarkProcessedAsync(message: message, ct: ct);

		logger.ZLogError(message: $"[{message.CorrelationId}] Recurring transaction {message.RecurringTransactionId} escalated to unresolvable_events. Reason: {reason}.");
	}

	private Task MarkProcessedAsync(RecurringTransactionTriggeredMessage message, CancellationToken ct)
	{
		return processedMessageWriteRepository.MarkAsProcessedAsync(
			messageId: message.MessageId,
			consumerType: nameof(RecurringTransactionConsumer),
			processedAt: dateProvider.UtcNow,
			ct: ct
		);
	}
}
