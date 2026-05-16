using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using ZLogger;

namespace FinanceTracker.Worker.RecurringTransactionProjection.Consumers;

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

			Core.Domains.RecurringTransaction.RecurringTransaction? recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(
				recurringTransactionId: message.RecurringTransactionId,
				ct: ct
			);

			if (recurringTransaction is null)
			{
				logger.ZLogWarning(message: $"[{message.CorrelationId}] Recurring transaction {message.RecurringTransactionId} not found.");
				return;
			}

			Account? account = await accountRepository.GetByIdAsync(accountId: message.AccountId, ct: ct);
			if (account is null)
			{
				logger.ZLogError(message: $"[{message.CorrelationId}] Account {message.AccountId} not found.");
				throw new NotFoundException(message: "Account not found.", id: message.AccountId);
			}

			Result<Currency, DomainException> currencyResult = Currency.Create(value: message.Currency);
			if (currencyResult.IsFailure)
				throw new NotFoundException(message: $"Invalid currency: {message.Currency}", id: message.RecurringTransactionId);

			await transactionCreationService.CreateAsync(command: new CreateTransactionCommand(
				AccountId: message.AccountId,
				UserId: message.UserId,
				CategoryId: message.CategoryId,
				Amount: message.Amount,
				Currency: currencyResult.Value,
				Direction: Enum.Parse<DirectionType>(value: message.Direction),
				Description: message.Description,
				OccurredAt: message.OccurredAt
			), account: account, ct: ct);

			await processedMessageWriteRepository.MarkAsProcessedAsync(
				messageId: message.MessageId,
				consumerType: nameof(RecurringTransactionConsumer),
				processedAt: dateProvider.UtcNow,
				ct: ct
			);

			logger.ZLogInformation(message: $"[{message.CorrelationId}] Created transaction for recurring transaction {message.RecurringTransactionId}.");
		}, ct: ct);
	}
}