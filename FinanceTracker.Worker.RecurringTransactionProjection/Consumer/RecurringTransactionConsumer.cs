using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.RecurringTransaction;
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
/// </summary>
public sealed class RecurringTransactionConsumer(
	IAccountRepository accountRepository,
	ITransactionCreationService transactionCreationService,
	IRecurringTransactionReadRepository recurringTransactionReadRepository,
	IProcessedMessageReadRepository processedMessageReadRepository,
	IProcessedMessageWriteRepository processedMessageWriteRepository,
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
				ct: ct
			);

			if (recurringTransaction is null)
			{
				logger.ZLogWarning(message: $"[{message.CorrelationId}] Recurring transaction {message.RecurringTransactionId} not found. Skipping.");
				await MarkProcessedAsync(message: message, ct: ct);
				return;
			}

			Account? account = await accountRepository.GetByIdAsync(accountId: message.AccountId, ct: ct);
			if (account is null)
			{
				logger.ZLogError(message: $"[{message.CorrelationId}] Account {message.AccountId} not found. Skipping.");
				await MarkProcessedAsync(message: message, ct: ct);
				return;
			}

			Result<Currency, DomainException> currencyResult = Currency.Create(value: message.Currency);
			if (currencyResult.IsFailure)
			{
				logger.ZLogError(message: $"[{message.CorrelationId}] Invalid currency '{message.Currency}' in message {message.MessageId}. Skipping.");
				await MarkProcessedAsync(message: message, ct: ct);
				return;
			}

			if (!Enum.TryParse(value: message.Direction, ignoreCase: true, result: out DirectionType direction))
			{
				logger.ZLogError(message: $"[{message.CorrelationId}] Invalid direction '{message.Direction}' in message {message.MessageId}. Skipping.");
				await MarkProcessedAsync(message: message, ct: ct);
				return;
			}

			Result<Guid, DomainException> result = await transactionCreationService.CreateAsync(command: new CreateTransactionCommand(
				AccountId: message.AccountId,
				UserId: message.UserId,
				CategoryId: message.CategoryId,
				Amount: message.Amount,
				Currency: currencyResult.Value,
				Direction: direction,
				Description: message.Description,
				OccurredAt: message.OccurredAt
			), account: account, ct: ct);

			if (result.IsSuccess)
				WorkerMetrics.TransactionsCreated.Add(delta: 1, new KeyValuePair<string, object?>(key: "direction", value: message.Direction));

			await MarkProcessedAsync(message: message, ct: ct);

			logger.ZLogInformation(message: $"[{message.CorrelationId}] Created transaction for recurring transaction {message.RecurringTransactionId}.");
		}, ct: ct);
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