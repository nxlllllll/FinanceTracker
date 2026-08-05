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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZLogger;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

/// <summary>
/// Consumes messages of type <typeparamref name="TMessage"/> from a RabbitMQ
/// queue and dispatches them to <typeparamref name="THandler"/>.
/// </summary>
public sealed class RabbitMqListenerService<TMessage, THandler>(
	RabbitMqConnectionFactory connectionFactory,
	IOptionsMonitor<RabbitMqOptions> options,
	IServiceScopeFactory scopeFactory,
	ILogger<RabbitMqListenerService<TMessage, THandler>> logger
) : RabbitMqConsumerBase<TMessage>(connectionFactory: connectionFactory, logger: logger)
	where TMessage : class
	where THandler : IMessageHandler<TMessage>
{
	private readonly string _routingKey = GetRoutingKey();
	private readonly string _queueName = RabbitMqQueueNaming.Resolve<THandler>(options: options.CurrentValue);

	internal string DeadLetterExchangeName => $"{_queueName}.dlx";
	internal string DeadLetterQueueName => $"{_queueName}.dlq";

	protected override string Description => "listener";
	protected override string QueueName => _queueName;
	protected override int PrefetchCount => options.CurrentValue.PrefetchCount;
	protected override int MaxReconnectDelaySeconds => options.CurrentValue.MaxReconnectDelaySeconds;

	private static string GetRoutingKey()
	{
		RoutingKeyAttribute? attribute = typeof(THandler).GetCustomAttribute<RoutingKeyAttribute>();
		return attribute?.RoutingKey ?? throw new InvalidOperationException(
			message: $"{typeof(THandler).Name} is missing [RoutingKey]."
		);
	}

	public override async Task StartAsync(CancellationToken ct)
	{
		RabbitMqOptions currentOptions = options.CurrentValue;
		logger.ZLogInformation(message: $"""
			[{typeof(TMessage).Name}] Listener starting. Queue: '{_queueName}',
			Exchange: '{currentOptions.ExchangeName}', RoutingKey: '{_routingKey}', MaxRetries: {currentOptions.MaxRetries}.
		""");
		await base.StartAsync(cancellationToken: ct);
	}

	protected override async Task DeclareTopologyAsync(CancellationToken ct)
	{
		RabbitMqOptions currentOptions = options.CurrentValue;

		await Channel.ExchangeDeclareAsync(
			exchange: currentOptions.ExchangeName,
			type: ExchangeType.Topic,
			durable: true,
			cancellationToken: ct
		);

		await DeclareDeadLetterInfrastructureAsync(ct: ct);

		await Channel.QueueDeclareAsync(
			queue: _queueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: new Dictionary<string, object?>
			{
				[RabbitMqQueueArguments.QueueType] = RabbitMqQueueArguments.QuorumQueueType,
				[RabbitMqQueueArguments.DeadLetterExchange] = DeadLetterExchangeName,
				[RabbitMqQueueArguments.DeliveryLimit] = currentOptions.MaxRetries,
				[RabbitMqQueueArguments.DelayedRetryType] = RabbitMqQueueArguments.FailedRetryType,
				[RabbitMqQueueArguments.DelayedRetryMin] = currentOptions.DelayedRetryMinMs,
				[RabbitMqQueueArguments.DelayedRetryMax] = currentOptions.DelayedRetryMaxMs
			},
			cancellationToken: ct
		);

		await Channel.QueueBindAsync(
			queue: _queueName,
			exchange: currentOptions.ExchangeName,
			routingKey: _routingKey,
			cancellationToken: ct
		);
	}

	/// <summary>
	/// Declares the per-queue dead-letter exchange/queue pair that <see cref="_queueName"/> routes into
	/// once its native <c>x-delivery-limit</c> is exceeded, or a delivery is rejected with
	/// <c>requeue: false</c> (see the <c>x-dead-letter-exchange</c> argument above).
	/// Also declared (idempotently) by <see cref="DeadLetterAuditListener{TMessage,THandler}"/>, since
	/// the two services start independently and neither should depend on start order.
	/// </summary>
	private async Task DeclareDeadLetterInfrastructureAsync(CancellationToken ct)
	{
		await Channel.ExchangeDeclareAsync(
			exchange: DeadLetterExchangeName,
			type: ExchangeType.Fanout,
			durable: true,
			cancellationToken: ct
		);

		await Channel.QueueDeclareAsync(
			queue: DeadLetterQueueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			cancellationToken: ct
		);

		await Channel.QueueBindAsync(
			queue: DeadLetterQueueName,
			exchange: DeadLetterExchangeName,
			routingKey: String.Empty,
			cancellationToken: ct
		);
	}

	protected override Task OnDeliveryFailedAsync(
		BasicDeliverEventArgs ea,
		Exception exception
	) => SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: true);

	protected override async Task HandleDeliveryAsync(
		object sender,
		BasicDeliverEventArgs ea,
		CancellationToken ct)
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
				utf8Json: ea.Body.Span,
				options: FinanceTrackerJsonOptions.Payload
			) ?? throw new InvalidOperationException(message: $"Failed to deserialize {typeof(TMessage).Name}.");
		}
		catch (Exception ex)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
			logger.ZLogError(exception: ex, message: $"{LogTag} deserialization failed for message {ea.DeliveryTag}. Rejecting to DLQ.");

			await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: false);
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
		RabbitMqOptions currentOptions = options.CurrentValue;

		try
		{
			await handler.HandleAsync(message: message, ct: ct);

			activity?.SetStatus(code: ActivityStatusCode.Ok);
			await SafeAckAsync(deliveryTag: ea.DeliveryTag);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: "Cancelled.");
			logger.ZLogWarning(message: $"{LogTag} processing cancelled for message {ea.DeliveryTag}. Requeuing without penalty.");
			await SafeNackAsync(deliveryTag: ea.DeliveryTag, requeue: true);
		}
		catch (Exception ex)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
			activity?.AddException(exception: ex);

			logger.ZLogWarning(exception: ex, message: $"""
				{LogTag} handler failed for message {ea.DeliveryTag}. Rejecting for native delayed retry
				(delivery-limit {currentOptions.MaxRetries}, {currentOptions.DelayedRetryMinMs}-{currentOptions.DelayedRetryMaxMs}ms linear backoff).
			""");

			await SafeRejectAsync(deliveryTag: ea.DeliveryTag, requeue: true);
		}
	}

	private static ActivityContext ExtractParentContext(IDictionary<string, object?>? headers)
	{
		if (headers is null)
			return default;

		string? traceParent = ReadHeader(headers: headers, key: "traceparent");

		if (traceParent is null)
			return default;

		string? traceState = ReadHeader(headers: headers, key: "tracestate");

		if (ActivityContext.TryParse(traceParent: traceParent, traceState: traceState, context: out ActivityContext context))
			return context;

		return default;
	}

	private static string? ReadHeader(IDictionary<string, object?> headers, string key)
	{
		if (!headers.TryGetValue(key: key, out object? value))
			return null;

		if (value is byte[] bytes)
			return Encoding.UTF8.GetString(bytes: bytes);

		return value as string;
	}
}
