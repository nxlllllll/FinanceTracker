using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Extensions;
using FinanceTracker.Infrastructure.Database.Jobs.Outbox;
using FinanceTracker.Worker.Shared.RabbitMQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quartz;
using RabbitMQ.Client;
using ZLogger;

namespace FinanceTracker.Worker.Outbox.Jobs;

[DisallowConcurrentExecution]
public sealed class OutboxPublisherJob(
    FinanceTrackerContext context,
    RabbitMqConnectionFactory connectionFactory,
    IUnitOfWork unitOfWork,
    IDateProvider dateProvider,
    IOptions<OutboxOptions> outboxOptions,
    IOptions<RabbitMqOptions> rabbitOptions,
    ILogger<OutboxPublisherJob> logger
) : IJob
{
    private readonly OutboxOptions _outboxOptions = outboxOptions.Value;
    private readonly RabbitMqOptions _rabbitOptions = rabbitOptions.Value;

    public async Task Execute(IJobExecutionContext executionContext)
    {
        await using IConnection connection = await connectionFactory.CreateConnectionAsync(ct: executionContext.CancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: executionContext.CancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _rabbitOptions.ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            cancellationToken: executionContext.CancellationToken
        );

        await ProcessBatchAsync(channel: channel, ct: executionContext.CancellationToken);
    }

    private async Task ProcessBatchAsync(IChannel channel, CancellationToken ct)
    {
        await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
        {
            List<OutboxMessageEntity> messages = await context.WithSkipLocked<OutboxMessageEntity>()
                .Where(predicate: m => m.ProcessedAt == null && m.FailedAt == null)
                .OrderBy(keySelector: m => m.UpdatedAt)
                .Take(count: _outboxOptions.BatchSize)
                .ToListAsync(cancellationToken: ct);

            if (messages.Count == 0)
                return;

            logger.ZLogInformation(message: $"Publishing {messages.Count} outbox message(s).");

            int published = 0;
            foreach (OutboxMessageEntity message in messages)
            {
                try
                {
                    await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
                    {
                        await PublishMessageAsync(channel: channel, message: message, ct: ct);
                        logger.ZLogInformation(message: $"Published: {++published}/{messages.Count}.");
                    }, ct: ct);
                }
                catch (Exception exception)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    logger.ZLogError(exception: exception, message: $"Failed to publish outbox message {message.Id}.");

                    await UpdateRetryStateAsync(message: message, ct: ct);
                }
            }
        }, onError: async exception => logger.ZLogError(exception: exception, message: $"Outbox batch publishing failed."), ct: ct);
    }

    private async Task PublishMessageAsync(IChannel channel, OutboxMessageEntity message, CancellationToken ct)
    {
        OutboxPayload payload = JsonSerializer.Deserialize<OutboxPayload>(json: message.Payload)
            ?? throw new SerializationException(message: "Failed to deserialize outbox payload.");

        AccountEventsMessage brokerMessage = new AccountEventsMessage(
            MessageId: message.Id,
            AggregateId: payload.AggregateId,
            Events: payload.Events.Select(selector: e => new AccountEventEnvelope(
                EventType: e.EventType,
                EventPayload: e.EventPayload
            )).ToList()
        );

        byte[] body = Encoding.UTF8.GetBytes(s: JsonSerializer.Serialize(value: brokerMessage, options: FinanceTrackerJsonOptions.Payload));

        await channel.BasicPublishAsync(
            exchange: _rabbitOptions.ExchangeName,
            routingKey: String.Empty,
            body: body,
            cancellationToken: ct
        );

        message.ProcessedAt = dateProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken: ct);
    }

    private async Task UpdateRetryStateAsync(OutboxMessageEntity message, CancellationToken ct)
    {
        try
        {
            await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
            {
                ++message.RetryCount;
                if (message.RetryCount >= _outboxOptions.MaxRetries)
                {
                    message.FailedAt = dateProvider.UtcNow;
                    logger.ZLogError(message: $"Outbox message {message.Id} moved to dead letter after {_outboxOptions.MaxRetries} retries.");
                }

                await context.SaveChangesAsync(cancellationToken: ct);
            }, ct: ct);
        }
        catch (Exception innerException)
        {
            logger.ZLogError(exception: innerException, message: $"Failed to update retry state for outbox message {message.Id}.");
        }
    }
}