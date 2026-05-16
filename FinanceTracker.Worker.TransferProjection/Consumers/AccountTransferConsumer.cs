using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using Microsoft.EntityFrameworkCore;
using ZLogger;

namespace FinanceTracker.Worker.TransferProjection.Consumers;

public sealed class AccountTransferConsumer(
	IAccountRepository accountRepository,
	IEventTypeResolver eventTypeResolver,
	IUnitOfWork unitOfWork,
	FinanceTrackerContext context,
	IDateProvider dateProvider,
	ILogger<AccountTransferConsumer> logger
) : IMessageHandler<AggregateEventsMessage>
{
	public async Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		if (message.AggregateType != AggregateTypeNames.Account)
			return;

		AccountTransferDebited? debitEvent = ExtractDebitEvent(message: message);
		if (debitEvent is null)
			return;

		using IDisposable? scope = logger.BeginScope(state: new Dictionary<string, object> { ["CorrelationId"] = message.CorrelationId });

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			bool alreadyProcessed = await context.ProcessedMessages.AnyAsync(
				predicate: m => m.MessageId == message.MessageId && m.ConsumerType == nameof(AccountTransferConsumer),
				cancellationToken: ct
			);

			if (alreadyProcessed)
			{
				logger.ZLogWarning(message: $"[{message.CorrelationId}] Message {message.MessageId} already processed.");
				return;
			}

			await ExecuteCreditAsync(debitEvent: debitEvent, correlationId: message.CorrelationId, ct: ct);

			await context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
			{
				MessageId = message.MessageId,
				ConsumerType = nameof(AccountTransferConsumer),
				ProcessedAt = dateProvider.UtcNow
			}, cancellationToken: ct);

			await context.SaveChangesAsync(cancellationToken: ct);
		}, ct: ct);
	}

	private async Task ExecuteCreditAsync(
		AccountTransferDebited debitEvent,
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

		logger.ZLogInformation(message: $"[{correlationId}] Transfer {debitEvent.TransferId} completed: {debitEvent.AccountId} → {debitEvent.ToAccountId}.");
	}

	private async Task CompensateAsync(
		AccountTransferDebited debitEvent,
		Guid correlationId,
		string? reason,
		CancellationToken ct)
	{
		Account? fromAccount = await accountRepository.GetByIdAsync(accountId: debitEvent.AccountId, ct: ct);

		if (fromAccount is null)
		{
			logger.ZLogError(message: $"[{correlationId}] Compensation FAILED: fromAccount {debitEvent.AccountId} not found. Transfer {debitEvent.TransferId} requires manual resolution.");
			return;
		}

		fromAccount.RefundTransfer(occurredAt: dateProvider.UtcNow, transferId: debitEvent.TransferId, amount: debitEvent.Amount, description: $"Refund: {reason}");

		await accountRepository.SaveAsync(account: fromAccount, ct: ct);

		logger.ZLogWarning(message: $"[{correlationId}] Compensation executed: refunded {debitEvent.Amount} to {debitEvent.AccountId} for transfer {debitEvent.TransferId}.");
	}

	private AccountTransferDebited? ExtractDebitEvent(AggregateEventsMessage message)
	{
		foreach (EventEnvelope envelope in message.Events)
		{
			try
			{
				Type type = eventTypeResolver.ResolveType(typeName: envelope.EventType);
				if (type != typeof(AccountTransferDebited))
					continue;

				return (AccountTransferDebited)JsonSerializer.Deserialize(
					json: envelope.EventPayload,
					returnType: type,
					options: FinanceTrackerJsonOptions.Payload
				)!;
			}
			catch (Exception exception)
			{
				logger.ZLogWarning(exception: exception, message: $"{exception.Message}");
			}
		}

		return null;
	}
}