using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Tracing;
using FinanceTracker.Core.Utilities.Retry;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Retry;
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
/// On handler failure, retries up to <see cref="RabbitMqOptions.MaxRetries"/> times
/// using <see cref="IRetryCounter"/> (Redis-backed with in-memory fallback).
/// </para>
/// <para>
/// A failed delivery that hasn't exhausted its retries is not immediately requeued. Instead, it is republished
/// into a per-queue "parking" queue (<c>{queue}.retry</c>) with a per-message TTL equal to the exponential
/// backoff delay (<see cref="RetryDelayCalculator"/>); once that TTL elapses, the broker dead-letters
/// it back into the original exchange/routing key, redelivering it to this same  listener
/// </para>
/// <para>
/// Messages that exhaust all retries are nacked without requeue, which RabbitMQ then routes —
/// via a dedicated per-queue dead-letter exchange (<c>{queue}.dlx</c>, fanout) — into
/// <c>{queue}.dlq</c>, where the <em>full, untouched</em> message body is preserved for manual
/// replay. A lightweight, searchable record (with a truncated body preview, for quick triage
/// without needing to inspect RabbitMQ directly) is also written to <c>unresolvable_events</c>.
/// </para>
/// </summary>
public sealed class RabbitMqListenerService<TMessage, THandler>(
	RabbitMqConnectionFactory connectionFactory,
	IOptions<RabbitMqOptions> options,
	IServiceScopeFactory scopeFactory,
	IRetryCounter retryCounter,
	ILogger<RabbitMqListenerService<TMessage, THandler>> logger
) : BackgroundService
	where TMessage : class
	where THandler : IMessageHandler<TMessage>
{
	public const string DeadLetterExchangeArgument = "x-dead-letter-exchange";
	public const string DeadLetterRoutingKeyArgument = "x-dead-letter-routing-key";

	private readonly RabbitMqOptions _options = options.Value;
	private readonly string _routingKey = GetRoutingKey();
	private readonly string _queueName = ResolveQueueName(options.Value);

	private IConnection? _connection;
	private IChannel? _channel;

	private string DeadLetterExchangeName => $"{_queueName}.dlx";
	private string DeadLetterQueueName => $"{_queueName}.dlq";
	private string RetryQueueName => $"{_queueName}.retry";

	private static string GetRoutingKey()
	{
		return typeof(TMessage).GetCustomAttribute<RoutingKeyAttribute>()?.RoutingKey
			?? throw new InvalidOperationException(message: $"{typeof(TMessage).Name} is missing [RabbitMqRoutingKey] attribute.");
	}

	/// <summary>
	/// Resolves the queue this listener actually binds/consumes from. Prefers a handler-specific override
	/// (see <see cref="RabbitMqOptions.QueueNameOverrides"/>) so that multiple listeners sharing one
	/// <see cref="RabbitMqOptions"/> section (e.g. a test host) each get their own queue instead of
	/// becoming competing consumers on the same one.
	/// </summary>
	private static string ResolveQueueName(RabbitMqOptions options)
	{
		if (options.QueueNameOverrides.TryGetValue(key: typeof(THandler).Name, out string? overrideName)
			&& !String.IsNullOrWhiteSpace(value: overrideName))
			return overrideName;

		return options.QueueName
			?? throw new InvalidOperationException(message: $"RabbitMQ:QueueName (or a QueueNameOverrides entry for '{typeof(THandler).Name}') must be configured.");
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
		await DeclareRetryInfrastructureAsync(ct: ct);

		await _channel.QueueDeclareAsync(
			queue: _queueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: new Dictionary<string, object?> { [DeadLetterExchangeArgument] = DeadLetterExchangeName },
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
	/// once retries are exhausted (see <see cref="ConnectAsync"/>'s <c>x-dead-letter-exchange</c> argument).
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

	private async Task DeclareRetryInfrastructureAsync(CancellationToken ct)
	{
		await _channel!.QueueDeclareAsync(
			queue: RetryQueueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: new Dictionary<string, object?>
			{
				[DeadLetterExchangeArgument] = _options.ExchangeName,
				[DeadLetterRoutingKeyArgument] = _routingKey
			},
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

		TMessage message;
		try
		{
			message = JsonSerializer.Deserialize<TMessage>(
				json: Encoding.UTF8.GetString(bytes: ea.Body.ToArray()),
				options: FinanceTrackerJsonOptions.Payload
			) ?? throw new InvalidOperationException(message: $"Failed to deserialize {typeof(TMessage).Name}.");
		}
		catch (Exception ex)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
			logger.ZLogError(exception: ex, message: $"[{typeof(TMessage).Name}] Deserialization failed for message {ea.DeliveryTag}. Discarding.");
			await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: false, ct: ct);
			return;
		}

		string messageKey = GetMessageKey(message: message, deliveryTag: ea.DeliveryTag);

		try
		{
			await handler.HandleAsync(message: message, ct: ct);

			activity?.SetStatus(code: ActivityStatusCode.Ok);
			await retryCounter.RemoveAsync(messageKey: messageKey, ct: ct);
			await SafeAckAsync(deliveryTag: ea.DeliveryTag, ct: ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: "Cancelled.");
			logger.ZLogWarning(message: $"[{typeof(TMessage).Name}] Processing cancelled for message {ea.DeliveryTag}. Requeuing.");
			await retryCounter.RemoveAsync(messageKey: messageKey, ct: ct);
			await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: true, ct: ct);
		}
		catch (Exception ex)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
			activity?.AddException(exception: ex);

			int deliveryCount = await retryCounter.IncrementAsync(messageKey: messageKey, ct: ct);
			bool isExhausted = deliveryCount >= _options.MaxRetries;

			if (isExhausted)
			{
				logger.ZLogError(
					exception: ex,
					message: $"[{typeof(TMessage).Name}] Handler failed for message {ea.DeliveryTag} after {deliveryCount + 1}/{_options.MaxRetries + 1} attempts. Sending to DLX."
				);

				await retryCounter.RemoveAsync(messageKey: messageKey, ct: ct);

				await RecordDeadLetterAsync(
					scope: scope,
					ea: ea,
					deliveryCount: deliveryCount,
					exception: ex,
					ct: ct
				);

				await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: false, ct: ct);
			}
			else
			{
				int delayMs = Math.Min(
					val1: RetryDelayCalculator.Calculate(
						attempt: deliveryCount,
						baseDelayMs: _options.RetryBaseDelayMs,
						useJitter: _options.RetryUseJitter
					),
					val2: _options.RetryMaxDelayMs
				);

				logger.ZLogWarning(exception: ex, message: $"""
					[{typeof(TMessage).Name}] Handler failed for message {ea.DeliveryTag} (attempt {deliveryCount + 1}/{_options.MaxRetries + 1}). Parking for {delayMs}ms.
				""");

				await ScheduleRetryAsync(ea: ea, delayMs: delayMs, ct: ct);
			}
		}
	}

	private static string GetMessageKey(TMessage message, ulong deliveryTag)
	{
		if (message is IRoutableMessage routable)
			return routable.MessageId.ToString();

		return deliveryTag.ToString();
	}

	/// <summary>
	/// Republishes the delivery into <see cref="RetryQueueName"/> with a per-message <c>Expiration</c>
	/// equal to <paramref name="delayMs"/>, preserving the original body, headers, and correlation id,
	/// then acks the original delivery.
	/// </summary>
	private async Task ScheduleRetryAsync(BasicDeliverEventArgs ea, int delayMs, CancellationToken ct)
	{
		try
		{
			BasicProperties retryProps = new BasicProperties
			{
				Headers = ea.BasicProperties.Headers,
				CorrelationId = ea.BasicProperties.CorrelationId,
				ContentType = ea.BasicProperties.ContentType,
				ContentEncoding = ea.BasicProperties.ContentEncoding,
				DeliveryMode = DeliveryModes.Persistent,
				Expiration = delayMs.ToString()
			};

			await _channel!.BasicPublishAsync(
				exchange: String.Empty,
				routingKey: RetryQueueName,
				mandatory: false,
				basicProperties: retryProps,
				body: ea.Body,
				cancellationToken: ct
			);

			await SafeAckAsync(deliveryTag: ea.DeliveryTag, ct: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"[{typeof(TMessage).Name}] Failed to park message {ea.DeliveryTag} for delayed retry. Requeuing immediately instead.");
			await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: true, ct: ct);
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

	/// <summary>See <see cref="SafeAckAsync"/> — same rationale, for the nack path.</summary>
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

	private async Task RecordDeadLetterAsync(
		AsyncServiceScope scope,
		BasicDeliverEventArgs ea,
		int deliveryCount,
		Exception exception,
		CancellationToken ct)
	{
		try
		{
			IUnresolvableEventWriteRepository repository = scope.ServiceProvider.GetRequiredService<IUnresolvableEventWriteRepository>();
			IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
			IDateProvider dateProvider = scope.ServiceProvider.GetRequiredService<IDateProvider>();

			string payload = JsonSerializer.Serialize(value: new
			{
				messageType = typeof(TMessage).Name,
				queue = _queueName,
				exchange = _options.ExchangeName,
				routingKey = _routingKey,
				deadLetterQueue = DeadLetterQueueName,
				deliveryTag = ea.DeliveryTag,
				retryCount = deliveryCount,
				exceptionType = exception.GetType().Name,
				exceptionMessage = exception.Message
			});

			await unitOfWork.ExecuteInTransactionAsync(operation: async () => await repository.CreateAsync(
				type: UnresolvableEventType.ConsumerDeadLetter,
				referenceId: Guid.CreateVersion7(),
				reason: $"Max retries ({_options.MaxRetries}) exceeded for {typeof(TMessage).Name}: {exception.Message}",
				payload: payload,
				occurredAt: dateProvider.UtcNow,
				ct: ct
			), ct: ct);
		}
		catch (Exception recordEx)
		{
			logger.ZLogError(
				exception: recordEx,
				message: $"[{typeof(TMessage).Name}] Failed to record dead letter in unresolvable_events for delivery tag {ea.DeliveryTag}."
			);
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