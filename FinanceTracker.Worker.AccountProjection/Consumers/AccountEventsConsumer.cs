using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Worker.AccountProjection.Converter;
using FinanceTracker.Worker.AccountProjection.Projection.Notifications;
using Microsoft.EntityFrameworkCore;
using ZLogger;

namespace FinanceTracker.Worker.AccountProjection.Consumers;

public sealed class AccountEventsConsumer(
    Projection.AccountProjection projection,
    IEventTypeResolver eventTypeResolver,
    FinanceTrackerContext context,
    IUnitOfWork unitOfWork,
    IDateProvider dateProvider,
    ILogger<AccountEventsConsumer> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { Converters = { new UtcDateTimeConverter() } };
    
    public async Task HandleAsync(AccountEventsMessage message, CancellationToken ct)
    {
        await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
        {
            bool alreadyProcessed = await context.ProcessedMessages.AnyAsync(
                predicate: m => m.MessageId == message.MessageId, 
                cancellationToken: ct
            );

            if (alreadyProcessed)
            {
                logger.ZLogWarning(message: $"Message {message.MessageId} already processed, skipping.");
                return;
            }

            List<IEvent> events = message.Events.Select(selector: e =>
            {
                Type type = eventTypeResolver.ResolveType(typeName: e.EventType);
                return (IEvent)JsonSerializer.Deserialize(json: e.EventPayload, returnType: type, options: JsonOptions)!;
            }).ToList();

            await projection.Handle(notification: new AccountEventsNotification(AccountId: message.AggregateId, Events: events), ct: ct);

            await context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
            {
                MessageId = message.MessageId,
                ProcessedAt = dateProvider.UtcNow
            }, cancellationToken: ct);

            await context.SaveChangesAsync(cancellationToken: ct);

            logger.ZLogInformation(message: $"Projected {events.Count} event(s) for Account {message.AggregateId}.");
        }, ct: ct);
    }
}