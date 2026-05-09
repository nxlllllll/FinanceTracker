using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Worker.AccountProjection.RabbitMQ;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZLogger;

namespace FinanceTracker.Worker.AccountProjection.Consumers;

public sealed class AccountEventsListenerService(
    RabbitMqConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<AccountEventsListenerService> logger
) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;
    private IConnection? _connection;
    private IChannel? _channel;

    public override async Task StartAsync(CancellationToken ct)
    {
        _connection = await connectionFactory.CreateConnectionAsync(ct: ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        await _channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            cancellationToken: ct
        );

        await _channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct
        );

        await _channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: String.Empty,
            cancellationToken: ct
        );

        await base.StartAsync(cancellationToken: ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel!);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            AccountEventsConsumer handler = scope.ServiceProvider.GetRequiredService<AccountEventsConsumer>();

            try
            {
                string json = Encoding.UTF8.GetString(bytes: ea.Body.ToArray());
                AccountEventsMessage message = JsonSerializer.Deserialize<AccountEventsMessage>(json: json)
                    ?? throw new InvalidOperationException(message: "Failed to deserialize AccountEventsMessage.");

                await handler.HandleAsync(message: message, ct: ct);
                await _channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.ZLogError(exception: ex, message: $"Failed to process message {ea.DeliveryTag}.");
                await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
        };

        await _channel!.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct
        );

        await Task.Delay(delay: Timeout.InfiniteTimeSpan, cancellationToken: ct);
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(cancellationToken: ct);
        if (_channel is not null) 
            await _channel.DisposeAsync();
        
        if (_connection is not null) 
            await _connection.DisposeAsync();
    }
}