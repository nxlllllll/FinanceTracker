using System.Text;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZLogger;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

/// <summary>
/// Consumes <c>{queue}.dlq</c> — the dead-letter queue paired with
/// <see cref="RabbitMqListenerService{TMessage,THandler}"/> for the same <typeparamref name="THandler"/> —
/// and records each dead-lettered message as a full-body, searchable entry in <c>unresolvable_events</c>.
/// </summary>
public sealed class DeadLetterAuditListener<TMessage, THandler>(
	RabbitMqConnectionFactory connectionFactory,
	IOptionsMonitor<RabbitMqOptions> options,
	IServiceScopeFactory scopeFactory,
	ILogger<DeadLetterAuditListener<TMessage, THandler>> logger
) : RabbitMqConsumerBase<TMessage>(connectionFactory: connectionFactory, logger: logger)
	where TMessage : class
	where THandler : IMessageHandler<TMessage>
{
	private readonly string _queueName = RabbitMqQueueNaming.Resolve<THandler>(options: options.CurrentValue);

	private string DeadLetterExchangeName => $"{_queueName}.dlx";
	private string DeadLetterQueueName => $"{_queueName}.dlq";

	protected override string Description => "dead-letter audit listener";
	protected override string QueueName => DeadLetterQueueName;
	protected override int PrefetchCount => options.CurrentValue.PrefetchCount;
	protected override int MaxReconnectDelaySeconds => options.CurrentValue.MaxReconnectDelaySeconds;

	public override async Task StartAsync(CancellationToken ct)
	{
		logger.ZLogInformation(message: $"[{typeof(TMessage).Name}] Dead-letter audit listener starting. Queue: '{DeadLetterQueueName}'.");
		await base.StartAsync(cancellationToken: ct);
	}

	/// <summary>
	/// Declares the same dead-letter exchange/queue pair as <see cref="RabbitMqListenerService{TMessage,THandler}"/>
	/// (idempotent — identical re-declaration is a no-op). Declared here too, rather than assumed to
	/// already exist, because the two services start independently and neither should depend on the
	/// other's start order.
	/// </summary>
	protected override async Task DeclareTopologyAsync(CancellationToken ct)
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

	protected override Task OnDeliveryFailedAsync(BasicDeliverEventArgs ea, Exception exception)
	{
		logger.ZLogWarning(message: $"{LogTag} left dead letter {ea.DeliveryTag} unacked; it will be redelivered.");
		return Task.CompletedTask;
	}

	protected override async Task HandleDeliveryAsync(object sender, BasicDeliverEventArgs ea, CancellationToken ct)
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

		await SafeAckAsync(deliveryTag: ea.DeliveryTag);
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
}
