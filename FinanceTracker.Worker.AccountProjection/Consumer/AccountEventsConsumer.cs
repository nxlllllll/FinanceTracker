using System.Text.Json;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities.Retry;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Worker.AccountProjection.Projection;
using FinanceTracker.Worker.AccountProjection.Projection.Notifications;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Worker.AccountProjection.Consumer;

/// <summary>
/// RabbitMQ message handler that receives <see cref="AggregateEventsMessage"/> from the account exchange,
/// deserializes each integration event, deduplicates via <c>processed_messages</c>,
/// and dispatches to <see cref="AccountProjection"/> via MediatR notification.
/// </summary>
public sealed class AccountEventsConsumer(
	Projection.AccountProjection projection,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	IProcessedMessageReadRepository processedMessageReadRepository,
	IProcessedMessageWriteRepository processedMessageWriteRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	IOptionsMonitor<ProjectionRetryOptions> retryOptions,
	ILogger<AccountEventsConsumer> logger
) : IMessageHandler<AggregateEventsMessage>
{
	public async Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		using IDisposable? scope = logger.BeginScope(state: new Dictionary<string, object> { ["CorrelationId"] = message.CorrelationId });

		List<IAccountIntegrationEvent> events = message.Events.Select(selector: e =>
		{
			Type type = integrationEventTypeResolver.ResolveType(eventType: e.EventType);
			return (IAccountIntegrationEvent)JsonSerializer.Deserialize(json: e.EventPayload, returnType: type, options: FinanceTrackerJsonOptions.Payload)!;
		}).ToList();

		ProjectionRetryOptions currentOptions = retryOptions.CurrentValue;

		await RetryDelayCalculator.ExecuteWithRetryAsync(
			operation: async innerCt =>
			{
				await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
				{
					if (await processedMessageReadRepository.IsProcessedAsync(messageId: message.MessageId, consumerType: nameof(AccountEventsConsumer), ct: innerCt))
					{
						logger.ZLogWarning(message: $"[{message.CorrelationId}] Message {message.MessageId} already processed.");
						return;
					}

					await projection.Handle(notification: new AccountEventsNotification(AccountId: message.AggregateId, Events: events), ct: innerCt);

					await processedMessageWriteRepository.MarkAsProcessedAsync(
						messageId: message.MessageId,
						consumerType: nameof(AccountEventsConsumer),
						processedAt: dateProvider.UtcNow,
						ct: innerCt
					);

					logger.ZLogInformation(message: $"[{message.CorrelationId}] Projected {events.Count} event(s) for Account {message.AggregateId}.");
				}, ct: innerCt);
			},
			logging: (exception, attempt, delay) => logger.ZLogWarning(
				exception: exception,
				message: $"[{message.CorrelationId}] Concurrency conflict projecting Account {message.AggregateId}. Retry {attempt + 1}/{currentOptions.MaxRetries} in {delay}ms."
			),
			exceptionFilter: ex => ex is ConcurrencyConflictException,
			maxRetries: currentOptions.MaxRetries,
			baseDelayMs: currentOptions.BaseDelayMs,
			useJitter: currentOptions.UseJitter,
			ct: ct
		);
	}
}