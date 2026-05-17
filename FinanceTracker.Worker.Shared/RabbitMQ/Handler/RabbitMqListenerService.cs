using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Tracing;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZLogger;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

public sealed class RabbitMqListenerService<TMessage, THandler>(
    RabbitMqConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqListenerService<TMessage, THandler>> logger
) : BackgroundService
    where TMessage : class
    where THandler : IMessageHandler<TMessage>
{
    private readonly RabbitMqOptions _options = options.Value;

    private IConnection? _connection;
    private IChannel? _channel;

    public override async Task StartAsync(CancellationToken ct)
    {
        logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Listener starting. Queue: '{_options.QueueName}', Exchange: '{_options.ExchangeName}'.");
        await base.StartAsync(cancellationToken: ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        int attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(ct: ct);

                attempt = 0;
                logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Connected successfully.");

                await ConsumeAsync(ct: ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                attempt++;
                int delaySeconds = Math.Min(val1: 30, val2: 1 << attempt);

                logger.ZLogError(exception: exception, message: $"[{typeof(TMessage).Name}] Connection failed (attempt {attempt}). Retrying in {delaySeconds}s.");

                await DisposeConnectionAsync();

                await Task.Delay(delay: TimeSpan.FromSeconds(value: delaySeconds), cancellationToken: ct);
            }
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
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
            queue: _options.QueueName!,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct
        );

        await _channel.QueueBindAsync(
            queue: _options.QueueName!,
            exchange: _options.ExchangeName,
            routingKey: String.Empty,
            cancellationToken: ct
        );
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        TaskCompletionSource connectionDropped = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

        _connection!.ConnectionShutdownAsync += (_, args) =>
        {
            logger.ZLogWarning(message: $"[{typeof(TMessage).Name}] Connection shutdown: {args.ReplyText}.");
            connectionDropped.TrySetResult();
            return Task.CompletedTask;
        };

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel!);
        consumer.ReceivedAsync += HandleMessageAsync;

        await _channel!.BasicConsumeAsync(
            queue: _options.QueueName!,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct
        );

        await using CancellationTokenRegistration reg = ct.Register(callback: () => connectionDropped.TrySetCanceled());

        await connectionDropped.Task;
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        ActivityContext parentContext = ExtractParentContext(headers: ea.BasicProperties?.Headers);

        using Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(
            name: $"rabbitmq.consume {typeof(TMessage).Name}",
            kind: ActivityKind.Consumer,
            parentContext
        );
        
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        ICorrelationContext? correlationContext = scope.ServiceProvider.GetService<ICorrelationContext>();

        if (correlationContext is not null && Guid.TryParse(input: ea.BasicProperties?.CorrelationId, result: out Guid correlationId))
            correlationContext.Set(correlationId: correlationId);

        THandler handler = scope.ServiceProvider.GetRequiredService<THandler>();

        try
        {
            TMessage message = JsonSerializer.Deserialize<TMessage>(
                json: Encoding.UTF8.GetString(bytes: ea.Body.ToArray()),
                options: FinanceTrackerJsonOptions.Payload
            ) ?? throw new InvalidOperationException(message: $"Failed to deserialize {typeof(TMessage).Name}.");

            await handler.HandleAsync(message: message, ct: CancellationToken.None);

            activity?.SetStatus(code: ActivityStatusCode.Ok);
            await _channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
            activity?.AddException(exception: ex);

            logger.ZLogError(exception: ex, message: $"[{typeof(TMessage).Name}] Failed to process message {ea.DeliveryTag}.");
            await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private static ActivityContext ExtractParentContext(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(key: "traceparent", out object? value))
            return default;

        string? traceparent = value is byte[] bytes ? Encoding.UTF8.GetString(bytes: bytes) : value as string;

        if (traceparent is null)
            return default;

        string[] parts = traceparent.Split(separator: '-');
        if (parts.Length != 4)
            return default;

        ActivityTraceId traceId = ActivityTraceId.CreateFromString(idData: parts[1]);
        
        ActivitySpanId spanId = ActivitySpanId.CreateFromString(idData: parts[2]);
        
        ActivityTraceFlags flags = parts[3] == "01" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

        return new ActivityContext(
            traceId: traceId,
            spanId: spanId,
            traceFlags: flags,
            isRemote: true
        );
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(cancellationToken: ct);
        await DisposeConnectionAsync();
        logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Listener stopped.");
    }

    private async Task DisposeConnectionAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}