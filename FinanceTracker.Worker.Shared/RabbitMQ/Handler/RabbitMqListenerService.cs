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
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Retry;
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
	IRetryCounter retryCounter,
	ILogger<RabbitMqListenerService<TMessage, THandler>> logger
) : BackgroundService
	where TMessage : class
	where THandler : IMessageHandler<TMessage>
{
	private readonly RabbitMqOptions _options = options.Value;
	private readonly string _routingKey = GetRoutingKey();

	private IConnection? _connection;
	private IChannel? _channel;

	private static string GetRoutingKey()
	{
		return typeof(TMessage).GetCustomAttribute<RoutingKeyAttribute>()?.RoutingKey
			?? throw new InvalidOperationException(message: $"{typeof(TMessage).Name} is missing [RabbitMqRoutingKey] attribute.");
	}

	public override async Task StartAsync(CancellationToken ct)
	{
		logger.ZLogInformation(message: $"""
			[{typeof(TMessage).Name}] Listener starting. Queue: '{_options.QueueName}', 
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

		await _channel.ExchangeDeclareAsync(
			exchange: _options.ExchangeName,
			type: ExchangeType.Topic,
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
			routingKey: _routingKey,
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

		AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel!);
		consumer.ReceivedAsync += (sender, ea) => HandleMessageAsync(sender: sender, ea: ea, ct: ct);

		await _channel!.BasicConsumeAsync(
			queue: _options.QueueName!,
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
			await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
			return;
		}

		string messageKey = GetMessageKey(message: message, deliveryTag: ea.DeliveryTag);

		try
		{
			await handler.HandleAsync(message: message, ct: ct);

			activity?.SetStatus(code: ActivityStatusCode.Ok);
			await retryCounter.RemoveAsync(messageKey: messageKey, ct: ct);
			await _channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: "Cancelled.");
			logger.ZLogWarning(message: $"[{typeof(TMessage).Name}] Processing cancelled for message {ea.DeliveryTag}. Requeuing.");
			await retryCounter.RemoveAsync(messageKey: messageKey, ct: ct);
			await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
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

				await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
			}
			else
			{
				logger.ZLogWarning(
					exception: ex,
					message: $"[{typeof(TMessage).Name}] Handler failed for message {ea.DeliveryTag} (attempt {deliveryCount + 1}/{_options.MaxRetries + 1}). Requeuing."
				);

				await _channel!.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct);
			}
		}
	}

	private static string GetMessageKey(TMessage message, ulong deliveryTag)
	{
		if (message is IRoutableMessage routable)
			return routable.MessageId.ToString();

		return deliveryTag.ToString();
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
			string bodyPreview = Encoding.UTF8.GetString(bytes: ea.Body.Span[..Math.Min(ea.Body.Length, 1024)]);

			string payload = JsonSerializer.Serialize(value: new
			{
				messageType = typeof(TMessage).Name,
				queue = _options.QueueName,
				exchange = _options.ExchangeName,
				routingKey = _routingKey,
				deliveryTag = ea.DeliveryTag,
				retryCount = deliveryCount,
				exceptionType = exception.GetType().Name,
				exceptionMessage = exception.Message,
				bodyPreview
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