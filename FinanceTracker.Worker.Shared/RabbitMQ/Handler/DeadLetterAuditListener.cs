using System.Text;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities.Retry;
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
/// Background service that consumes <c>{queue}.dlq</c> — the dead-letter queue paired with
/// <see cref="RabbitMqListenerService{TMessage,THandler}"/> for the same <typeparamref name="THandler"/> —
/// and records each dead-lettered message as a full-body, searchable entry in <c>unresolvable_events</c>.
/// </summary>
public sealed class DeadLetterAuditListener<TMessage, THandler>(
	RabbitMqConnectionFactory connectionFactory,
	IOptions<RabbitMqOptions> options,
	IServiceScopeFactory scopeFactory,
	ILogger<DeadLetterAuditListener<TMessage, THandler>> logger
) : BackgroundService
	where TMessage : class
	where THandler : IMessageHandler<TMessage>
{
	private const int MaxReconnectDelaySeconds = 30;
	private readonly string _queueName = RabbitMqQueueNaming.Resolve<THandler>(options: options.Value);

	private IConnection? _connection;
	private IChannel? _channel;

	private string DeadLetterExchangeName => $"{_queueName}.dlx";
	private string DeadLetterQueueName => $"{_queueName}.dlq";

	public override async Task StartAsync(CancellationToken ct)
	{
		logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Dead-letter audit listener starting. Queue: '{DeadLetterQueueName}'.");
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
				logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Dead-letter audit listener connected successfully.");

				await ConsumeAsync(ct: ct);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				attempt++;
				int delaySeconds = RetryDelayCalculator.CalculateSeconds(attempt: attempt, maxSeconds: MaxReconnectDelaySeconds);

				logger.ZLogError(exception: exception, message: $"""
					[{typeof(TMessage).Name}] Dead-letter audit listener connection failed (attempt {attempt}). Retrying in {delaySeconds}s.
				""");

				await DisposeConnectionAsync();

				await Task.Delay(delay: TimeSpan.FromSeconds(value: delaySeconds), cancellationToken: ct);
			}
		}
	}

	/// <summary>
	/// Declares the same dead-letter exchange/queue pair as <see cref="RabbitMqListenerService{TMessage,THandler}"/>
	/// (idempotent — identical re-declaration is a no-op). Declared here too, rather than assumed to
	/// already exist, because the two services start independently and neither should depend on the
	/// other's start order.
	/// </summary>
	private async Task ConnectAsync(CancellationToken ct)
	{
		_connection = await connectionFactory.CreateConnectionAsync(ct: ct);
		RabbitMqVersionGuard.EnsureSupportedVersion(connection: _connection);

		_channel = await _connection.CreateChannelAsync(cancellationToken: ct);

		await _channel.BasicQosAsync(
			prefetchSize: 0,
			prefetchCount: 10,
			global: false,
			cancellationToken: ct
		);

		await _channel.ExchangeDeclareAsync(
			exchange: DeadLetterExchangeName,
			type: ExchangeType.Fanout,
			durable: true,
			cancellationToken: ct
		);

		await _channel.QueueDeclareAsync(
			queue: DeadLetterQueueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			cancellationToken: ct
		);

		await _channel.QueueBindAsync(
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
			logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Dead-letter audit listener connection shutdown: {args.ReplyText}.");
			connectionDropped.TrySetResult();
			return Task.CompletedTask;
		};

		_channel!.ChannelShutdownAsync += (_, args) =>
		{
			logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Dead-letter audit listener channel shutdown: {args.ReplyText}.");
			connectionDropped.TrySetResult();
			return Task.CompletedTask;
		};

		AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel!);
		consumer.ReceivedAsync += async (_, ea) =>
		{
			try
			{
				await HandleDeadLetterAsync(ea: ea, ct: ct);
			}
			catch (Exception ex)
			{
				logger.ZLogError(exception: ex, message: $"[{typeof(TMessage).Name}] Unhandled exception recording dead letter {ea.DeliveryTag}. Leaving unacked for retry.");
			}
		};

		await _channel!.BasicConsumeAsync(
			queue: DeadLetterQueueName,
			autoAck: false,
			consumer: consumer,
			cancellationToken: ct
		);

		await using CancellationTokenRegistration reg = ct.Register(callback: () => connectionDropped.TrySetCanceled());
		await connectionDropped.Task;
	}

	private async Task HandleDeadLetterAsync(BasicDeliverEventArgs ea, CancellationToken ct)
	{
		try
		{
			await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

			IUnresolvableEventWriteRepository repository = scope.ServiceProvider.GetRequiredService<IUnresolvableEventWriteRepository>();
			IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
			IDateProvider dateProvider = scope.ServiceProvider.GetRequiredService<IDateProvider>();

			(string reason, string? originalQueue, long? deathCount) = ExtractDeathInfo(headers: ea.BasicProperties.Headers);
			string fullBody = Encoding.UTF8.GetString(bytes: ea.Body.ToArray());

			Guid referenceId = Guid.TryParse(input: ea.BasicProperties.MessageId, result: out Guid messageId) ? messageId : Guid.CreateVersion7();

			string payload = JsonSerializer.Serialize(value: new
			{
				messageType = typeof(TMessage).Name,
				deadLetterQueue = DeadLetterQueueName,
				originalQueue,
				deathCount,
				deliveryTag = ea.DeliveryTag,
				messageId = ea.BasicProperties.MessageId,
				correlationId = ea.BasicProperties.CorrelationId,
				body = fullBody
			});

			await unitOfWork.ExecuteInTransactionAsync(operation: async () => await repository.CreateAsync(
				type: UnresolvableEventType.ConsumerDeadLetter,
				referenceId: referenceId,
				reason: reason,
				payload: payload,
				occurredAt: dateProvider.UtcNow,
				ct: ct
			), ct: ct);

			await _channel!.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: ct);
		}
		catch (Exception ex) when (ex is AlreadyClosedException or ObjectDisposedException)
		{
			logger.ZLogWarning(exception: ex, message: $"[{typeof(TMessage).Name}] Failed to ack dead letter {ea.DeliveryTag}: channel already closed.");
		}
	}

	/// <summary>
	/// Reads RabbitMQ's own <c>x-death</c> header array — attached automatically to every dead-lettered
	/// message — for the authoritative reason, originating queue, and redelivery count. Falls back to a
	/// generic reason if the header is absent or in an unexpected shape (defensive; should not normally happen).
	/// </summary>
	private static (string Reason, string? OriginalQueue, long? DeathCount) ExtractDeathInfo(IDictionary<string, object?>? headers)
	{
		if (
			headers is null || !headers.TryGetValue(key: "x-death", out object? xDeath) ||
			xDeath is not List<object> deaths || deaths.Count == 0 ||
			deaths[0] is not IDictionary<string, object> first
		) return ("Dead-lettered (no x-death header present).", null, null);

		string reasonText = first.TryGetValue(key: "reason", out object? reasonValue) && reasonValue is byte[] reasonBytes
			? Encoding.UTF8.GetString(bytes: reasonBytes)
			: "unknown";

		string? originalQueue = first.TryGetValue(key: "queue", out object? queueValue) && queueValue is byte[] queueBytes
			? Encoding.UTF8.GetString(bytes: queueBytes)
			: null;

		long? deathCount = first.TryGetValue(key: "count", out object? countValue) && countValue is long count
			? count
			: null;

		string reason = $"Dead-lettered from '{originalQueue ?? "unknown"}': {reasonText}" + (deathCount is not null ? $" (x-death count: {deathCount})." : ".");

		return (reason, originalQueue, deathCount);
	}

	public override async Task StopAsync(CancellationToken ct)
	{
		await base.StopAsync(cancellationToken: ct);
		await DisposeConnectionAsync();
		logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Dead-letter audit listener stopped.");
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
