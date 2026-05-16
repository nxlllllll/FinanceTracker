using System.Text.Json;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Worker.AccountProjection.Projection.Notifications;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using ZLogger;

namespace FinanceTracker.Worker.AccountProjection.Consumers;

public sealed class AccountEventsConsumer(
	Projection.AccountProjection projection,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	IProcessedMessageReadRepository processedMessageReadRepository,
	IProcessedMessageWriteRepository processedMessageWriteRepository,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	ILogger<AccountEventsConsumer> logger
) : IMessageHandler<AggregateEventsMessage>
{
	public async Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		if (message.AggregateType != AggregateTypeNames.Account)
		{
			logger.ZLogDebug(message: $"[{message.CorrelationId}] Skipping '{message.AggregateType}'.");
			return;
		}

		using IDisposable? scope = logger.BeginScope(state: new Dictionary<string, object> { ["CorrelationId"] = message.CorrelationId });

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			if (await processedMessageReadRepository.IsProcessedAsync(messageId: message.MessageId, consumerType: nameof(AccountEventsConsumer), ct: ct))
			{
				logger.ZLogWarning(message: $"[{message.CorrelationId}] Message {message.MessageId} already processed.");
				return;
			}

			List<IAccountIntegrationEvent> events = message.Events.Select(selector: e =>
			{
				Type type = integrationEventTypeResolver.ResolveType(eventType: e.EventType);
				return (IAccountIntegrationEvent)JsonSerializer.Deserialize(json: e.EventPayload, returnType: type, options: FinanceTrackerJsonOptions.Payload)!;
			}).ToList();

			await projection.Handle(notification: new AccountEventsNotification(AccountId: message.AggregateId, Events: events), ct: ct);

			await processedMessageWriteRepository.MarkAsProcessedAsync(
				messageId: message.MessageId,
				consumerType: nameof(AccountEventsConsumer),
				processedAt: dateProvider.UtcNow,
				ct: ct
			);

			logger.ZLogInformation(message: $"[{message.CorrelationId}] Projected {events.Count} event(s) for Account {message.AggregateId}.");
		}, ct: ct);
	}
}