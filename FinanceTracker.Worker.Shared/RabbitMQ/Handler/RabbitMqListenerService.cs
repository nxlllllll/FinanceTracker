using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.Tracing;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using ZLogger;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

/// <summary>
/// Background service that consumes messages of type <typeparamref name="TMessage"/>
/// from a RabbitMQ queue and dispatches them to <typeparamref name="THandler"/>.
/// <para>
/// Handles connection recovery automatically with exponential backoff.
/// </para>
/// </summary>
public sealed class RabbitMqListenerService<TMessage, THandler>(
	RabbitMqConnectionFactory connectionFactory,
	IOptions<RabbitMqOptions> options,
	IServiceScopeFactory scopeFactory,
	ILogger<RabbitMqListenerService<TMessage, THandler>> logger
) : BackgroundService
	where TMessage : class
	where THandler : IMessageHandler<TMessage>
{
	public const string QueueTypeArgument = "x-queue-type";
	public const string DeliveryLimitArgument = "x-delivery-limit";
	public const string DelayedRetryTypeArgument = "x-delayed-retry-type";
	public const string DelayedRetryMinArgument = "x-delayed-retry-min";
	public const string DelayedRetryMaxArgument = "x-delayed-retry-max";
	public const string DeadLetterExchangeArgument = "x-dead-letter-exchange";

	private readonly RabbitMqOptions _options = options.Value;
	private readonly string _routingKey = GetRoutingKey();
	private readonly string _queueName = RabbitMqQueueNaming.Resolve<THandler>(options: options.Value);

	private IConnection? _connection;
	private IChannel? _channel;

	internal string DeadLetterExchangeName => $"{_queueName}.dlx";
	internal string DeadLetterQueueName => $"{_queueName}.dlq";

	private static string GetRoutingKey()
	{
		RoutingKeyAttribute? attribute = typeof(TMessage).GetCustomAttribute<RoutingKeyAttribute>();
		return attribute?.RoutingKey ?? throw new InvalidOperationException(
			message: $"{typeof(TMessage).Name} is missing [RoutingKey]."
		);
	}

	public override async Task StartAsync(CancellationToken ct)
	{
		logger.ZLogInformation(message: $"""
			[{typeof(TMessage).Name}] Listener starting. Queue: '{_queueName}', 
			Exchange: '{_options.ExchangeName}', RoutingKey: '{_routingKey}', MaxRetries: {_options.MaxRetries}.
		""");
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

		await _channel.BasicQosAsync(
			prefetchSize: 0,
			prefetchCount: (ushort)_options.PrefetchCount,
			global: false,
			cancellationToken: ct
		);

		await _channel.ExchangeDeclareAsync(
			exchange: _options.ExchangeName,
			type: ExchangeType.Topic,
			durable: true,
			cancellationToken: ct
		);

		await DeclareDeadLetterInfrastructureAsync(ct: ct);

		await _channel.QueueDeclareAsync(
			queue: _queueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: new Dictionary<string, object?>
			{
				[QueueTypeArgument] = "quorum",
				[DeadLetterExchangeArgument] = DeadLetterExchangeName,
				[DeliveryLimitArgument] = _options.MaxRetries,
				[DelayedRetryTypeArgument] = "failed",
				[DelayedRetryMinArgument] = _options.DelayedRetryMinMs,
				[DelayedRetryMaxArgument] = _options.DelayedRetryMaxMs
			},
			cancellationToken: ct
		);

		await _channel.QueueBindAsync(
			queue: _queueName,
			exchange: _options.ExchangeName,
			routingKey: _routingKey,
			cancellationToken: ct
		);
	}

	/// <summary>
	/// Declares the per-queue dead-letter exchange/queue pair that <see cref="_queueName"/> routes into
	/// once its native <c>x-delivery-limit</c> is exceeded, or a delivery is rejected with
	/// <c>requeue: false</c> (see <see cref="ConnectAsync"/>'s <c>x-dead-letter-exchange</c> argument).
	/// Also declared (idempotently) by <see cref="DeadLetterAuditListener{TMessage,THandler}"/>, since
	/// the two services start independently and neither should depend on start order.
	/// </summary>
	private async Task DeclareDeadLetterInfrastructureAsync(CancellationToken ct)
	{
		await _channel!.ExchangeDeclareAsync(
			exchange: DeadLetterExchangeName,
			type: ExchangeType.Fanout,
			durable: true,
			cancellationToken: ct
		);

		await _channel!.QueueDeclareAsync(
			queue: DeadLetterQueueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			cancellationToken: ct
		);

		await _channel!.QueueBindAsync(
			queue: DeadLetterQueueName,
			exchange: DeadLetterExchangeName,
			routingKey: String.Empty,
			cancellationToken: ct
		);
	}

	private async Task ConsumeAsync(CancellationToken ct)
	{
		TaskCompletionSource connectionDropped = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

		_connection!.ConnectionShutdownAsync += (_, args) =>
		{
			logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Connection shutdown: {args.ReplyText}.");
			connectionDropped.TrySetResult();
			return Task.CompletedTask;
		};

		_channel!.ChannelShutdownAsync += (_, args) =>
		{
			logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Channel shutdown: {args.ReplyText}.");
			connectionDropped.TrySetResult();
			return Task.CompletedTask;
		};

		AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel!);
		consumer.ReceivedAsync += async (sender, ea) =>
		{
			try
			{
				await HandleMessageAsync(sender: sender, ea: ea, ct: ct);
			}
			catch (Exception ex)
			{
				logger.ZLogError(exception: ex, message: $"[{typeof(TMessage).Name}] Unhandled exception processing delivery {ea.DeliveryTag}.");
				await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: true, ct: ct);
			}
		};

		await _channel!.BasicConsumeAsync(
			queue: _queueName,
			autoAck: false,
			consumer: consumer,
			cancellationToken: ct
		);

		await using CancellationTokenRegistration reg = ct.Register(callback: () => connectionDropped.TrySetCanceled());
		await connectionDropped.Task;
	}

	private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs ea, CancellationToken ct)
	{
		ActivityContext parentContext = ExtractParentContext(headers: ea.BasicProperties?.Headers);

		using Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(
			name: $"{FinanceTrackerActivitySource.Operations.RabbitMqConsume} {typeof(TMessage).Name}",
			kind: ActivityKind.Consumer,
			parentContext
		);

		await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

		ICorrelationContext? correlationContext = scope.ServiceProvider.GetService<ICorrelationContext>();

		if (correlationContext is not null && Guid.TryParse(input: ea.BasicProperties?.CorrelationId, result: out Guid correlationId))
			correlationContext.Set(correlationId: correlationId);

		THandler handler = scope.ServiceProvider.GetRequiredService<THandler>();

		TMessage? message = await DeserializeMessageAsync(ea: ea, activity: activity, ct: ct);
		if (message is null)
			return;

		await DispatchAsync(handler: handler, message: message, ea: ea, activity: activity, ct: ct);
	}

	/// <summary>
	/// Deserializes the delivery body into <typeparamref name="TMessage"/>. On failure, rejects it
	/// without requeue — straight to DLQ via <see cref="DeadLetterExchangeArgument"/> — since a
	/// malformed payload will never deserialize on redelivery, so retrying it is pointless.
	/// <see cref="DeadLetterAuditListener{TMessage,THandler}"/> records it once it lands in the DLQ.
	/// Returns <c>null</c> on failure; the caller must treat that as "already handled, stop here".
	/// </summary>
	private async Task<TMessage?> DeserializeMessageAsync(BasicDeliverEventArgs ea, Activity? activity, CancellationToken ct)
	{
		try
		{
			return JsonSerializer.Deserialize<TMessage>(
				json: Encoding.UTF8.GetString(bytes: ea.Body.ToArray()),
				options: FinanceTrackerJsonOptions.Payload
			) ?? throw new InvalidOperationException(message: $"Failed to deserialize {typeof(TMessage).Name}.");
		}
		catch (Exception ex)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
			logger.ZLogError(exception: ex, message: $"[{typeof(TMessage).Name}] Deserialization failed for message {ea.DeliveryTag}. Rejecting to DLQ.");

			await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: false, ct: ct);
			return null;
		}
	}

	/// <summary>
	/// Invokes <paramref name="handler"/> and reacts to the outcome: ack on success, requeue without
	/// penalty on cooperative cancellation (doesn't count toward <c>x-delivery-limit</c>, since it
	/// isn't a real processing failure), or reject with <c>requeue: true</c> on any other failure —
	/// letting the quorum queue's native delayed-retry apply backoff and, once <c>x-delivery-limit</c>
	/// is exceeded, dead-letter it automatically.
	/// </summary>
	private async Task DispatchAsync(
		THandler handler,
		TMessage message,
		BasicDeliverEventArgs ea,
		Activity? activity,
		CancellationToken ct)
	{
		try
		{
			await handler.HandleAsync(message: message, ct: ct);

			activity?.SetStatus(code: ActivityStatusCode.Ok);
			await SafeAckAsync(deliveryTag: ea.DeliveryTag, ct: ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: "Cancelled.");
			logger.ZLogWarning(message: $"[{typeof(TMessage).Name}] Processing cancelled for message {ea.DeliveryTag}. Requeuing without penalty.");
			await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: true, ct: ct);
		}
		catch (Exception ex)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
			activity?.AddException(exception: ex);

			logger.ZLogWarning(exception: ex, message: $"""
				[{typeof(TMessage).Name}] Handler failed for message {ea.DeliveryTag}. Rejecting for native 
				delayed retry (delivery-limit {_options.MaxRetries}, {_options.DelayedRetryMinMs}-{_options.DelayedRetryMaxMs}ms linear backoff).
			""");

			await SafeRejectAsync(deliveryTag: ea.DeliveryTag, requeue: true, ct: ct);
		}
	}

	/// <summary>
	/// Acks a delivery, swallowing <see cref="AlreadyClosedException"/>/<see cref="ObjectDisposedException"/>.
	/// If the channel already closed concurrently, RabbitMQ will automatically requeue the unacked
	/// delivery once it notices the channel/connection is gone — the <c>ChannelShutdownAsync</c>/
	/// <c>ConnectionShutdownAsync</c> handlers in <see cref="ConsumeAsync"/> will then trigger a
	/// reconnect. Letting this exception propagate unhandled out of an AsyncEventingBasicConsumer
	/// event handler would otherwise just vanish silently without ever surfacing in logs.
	/// </summary>
	private async Task SafeAckAsync(ulong deliveryTag, CancellationToken ct)
	{
		try
		{
			await _channel!.BasicAckAsync(deliveryTag: deliveryTag, multiple: false, cancellationToken: ct);
		}
		catch (Exception ex) when (ex is AlreadyClosedException or ObjectDisposedException)
		{
			logger.ZLogWarning(exception: ex, message: $"[{typeof(TMessage).Name}] Ack failed for delivery {deliveryTag}: channel already closed.");
		}
	}

	/// <summary>
	/// See <see cref="SafeAckAsync"/> — same rationale, for the nack path. Used specifically where a
	/// requeue must <b>not</b> count toward the quorum queue's <c>x-delivery-limit</c> (cooperative
	/// cancellation), since <c>basic.nack(requeue: true)</c> is treated by RabbitMQ 4.3+ as an
	/// application-level "explicit return" rather than a failed redelivery.
	/// </summary>
	private async Task SafeNackAsync(ulong deliveryTag, bool requeue, CancellationToken ct)
	{
		try
		{
			await _channel!.BasicNackAsync(deliveryTag: deliveryTag, multiple: false, requeue: requeue, cancellationToken: ct);
		}
		catch (Exception ex) when (ex is AlreadyClosedException or ObjectDisposedException)
		{
			logger.ZLogWarning(exception: ex, message: $"[{typeof(TMessage).Name}] Nack failed for delivery {deliveryTag}: channel already closed.");
		}
	}

	/// <summary>
	/// See <see cref="SafeAckAsync"/> — same rationale, for the reject path. Used for genuine handler
	/// failures: unlike <see cref="SafeNackAsync"/>, <c>basic.reject</c> counts toward the quorum
	/// queue's <c>x-delivery-limit</c> and triggers its native delayed-retry backoff.
	/// </summary>
	private async Task SafeRejectAsync(ulong deliveryTag, bool requeue, CancellationToken ct)
	{
		try
		{
			await _channel!.BasicRejectAsync(deliveryTag: deliveryTag, requeue: requeue, cancellationToken: ct);
		}
		catch (Exception ex) when (ex is AlreadyClosedException or ObjectDisposedException)
		{
			logger.ZLogWarning(exception: ex, message: $"[{typeof(TMessage).Name}] Reject failed for delivery {deliveryTag}: channel already closed.");
		}
	}

	private static ActivityContext ExtractParentContext(IDictionary<string, object?>? headers)
	{
		if (headers is null || !headers.TryGetValue(key: FinanceTrackerActivitySource.TraceContextHeaders.TraceParent, out object? value))
			return default;

		string? traceparent = value is byte[] bytes ? Encoding.UTF8.GetString(bytes: bytes) : value as string;

		if (traceparent is null)
			return default;

		string[] parts = traceparent.Split(separator: '-');
		if (parts.Length != 4)
			return default;

		try
		{
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
		catch (Exception)
		{
			return default;
		}
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
