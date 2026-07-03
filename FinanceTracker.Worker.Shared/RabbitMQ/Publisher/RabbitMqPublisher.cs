using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Tracing;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZLogger;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Publisher;

/// <summary>
/// Publishes <see cref="IRoutableMessage"/> instances to a RabbitMQ topic exchange.
/// Concurrent callers never race to create duplicate connections, and the cached channel
/// is re-validated via <see cref="IChannel.IsOpen"/> before reuse so a dropped connection
/// (broker restart, network blip) is transparently recreated rather than silently reused forever. 
/// Propagates the W3C <c>traceparent</c> header for distributed tracing across service boundaries.
/// </summary>
public sealed class RabbitMqPublisher(
	RabbitMqConnectionFactory connectionFactory,
	IOptions<RabbitMqOptions> options,
	IServiceScopeFactory scopeFactory,
	ILogger<RabbitMqPublisher> logger
) : IRabbitMqPublisher
{
	private readonly RabbitMqOptions _options = options.Value;
	private readonly SemaphoreSlim _channelLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);
	private IConnection? _connection;
	private IChannel? _channel;

	public async Task PublishAsync<TMessage>(
		TMessage message,
		Guid? correlationId = default,
		CancellationToken ct = default) where TMessage : class, IRoutableMessage
	{
		IChannel channel = await GetOrCreateChannelAsync(ct: ct);

		byte[] body = Encoding.UTF8.GetBytes(s: JsonSerializer.Serialize(value: message, options: FinanceTrackerJsonOptions.Payload));

		BasicProperties props = new BasicProperties()
		{
			DeliveryMode = DeliveryModes.Persistent
		};

		if (correlationId is not null && correlationId != Guid.Empty)
			props.CorrelationId = correlationId.ToString();

		if (Activity.Current is { } current)
		{
			props.Headers ??= new Dictionary<string, object?>();
			props.Headers[FinanceTrackerActivitySource.TraceContextHeaders.TraceParent] = Encoding.UTF8.GetBytes(
				s: $"00-{current.TraceId}-{current.SpanId}-{(current.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00")}"
			);

			if (!String.IsNullOrEmpty(value: current.TraceStateString))
				props.Headers[FinanceTrackerActivitySource.TraceContextHeaders.TraceState] = Encoding.UTF8.GetBytes(s: current.TraceStateString);
		}

		await channel.BasicPublishAsync(
			exchange: _options.ExchangeName,
			routingKey: message.RoutingKey,
			mandatory: true,
			basicProperties: props,
			body: body,
			cancellationToken: ct
		);
	}

	private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken ct)
	{
		if (_channel is { IsOpen: true })
			return _channel;

		await _channelLock.WaitAsync(cancellationToken: ct);

		try
		{
			if (_channel is { IsOpen: true })
				return _channel;

			await DisposeStaleConnectionAsync();

			_connection = await connectionFactory.CreateConnectionAsync(ct: ct);

			_channel = await _connection.CreateChannelAsync(
				options: new CreateChannelOptions(
					publisherConfirmationsEnabled: true,
					publisherConfirmationTrackingEnabled: true
				),
				cancellationToken: ct
			);

			_channel.BasicReturnAsync += OnBasicReturnAsync;

			await _channel.ExchangeDeclareAsync(
				exchange: _options.ExchangeName,
				type: ExchangeType.Topic,
				durable: true,
				cancellationToken: ct
			);

			return _channel;
		}
		finally
		{
			_channelLock.Release();
		}
	}

	/// <summary>
	/// Invoked by the broker when a <c>mandatory</c> publish could not be routed to any queue.
	/// This fires asynchronously and independently of <see cref="PublishAsync{TMessage}"/>'s own
	/// Task, which may already have completed by the time this runs — so the only options here
	/// are to make the failure visible (log + <c>unresolvable_events</c>), not to reject the
	/// original call.
	/// </summary>
	private async Task OnBasicReturnAsync(object sender, BasicReturnEventArgs args)
	{
		logger.ZLogError(message: $"""
			[RabbitMqPublisher] Message returned as unroutable: exchange='{args.Exchange}', 
			routingKey='{args.RoutingKey}', replyCode={args.ReplyCode}, replyText='{args.ReplyText}'.
		""");

		await RecordUnroutableMessageAsync(args: args);
	}

	private async Task RecordUnroutableMessageAsync(BasicReturnEventArgs args)
	{
		try
		{
			await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

			IUnresolvableEventWriteRepository repository = scope.ServiceProvider.GetRequiredService<IUnresolvableEventWriteRepository>();
			IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
			IDateProvider dateProvider = scope.ServiceProvider.GetRequiredService<IDateProvider>();

			string payload = JsonSerializer.Serialize(value: new
			{
				exchange = args.Exchange,
				routingKey = args.RoutingKey,
				replyCode = args.ReplyCode,
				replyText = args.ReplyText,
				correlationId = args.BasicProperties?.CorrelationId
			});

			await unitOfWork.ExecuteInTransactionAsync(operation: async () => await repository.CreateAsync(
				type: UnresolvableEventType.PublisherUnroutable,
				referenceId: Guid.CreateVersion7(),
				reason: $"Message unroutable: exchange='{args.Exchange}', routingKey='{args.RoutingKey}' (broker reply {args.ReplyCode}: {args.ReplyText}).",
				payload: payload,
				occurredAt: dateProvider.UtcNow,
				ct: CancellationToken.None
			), ct: CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"[RabbitMqPublisher] Failed to record unroutable message in unresolvable_events.");
		}
	}

	private async Task DisposeStaleConnectionAsync()
	{
		if (_channel is not null)
		{
			_channel.BasicReturnAsync -= OnBasicReturnAsync;
			await _channel.DisposeAsync();
			_channel = null;
		}

		if (_connection is not null)
		{
			await _connection.DisposeAsync();
			_connection = null;
		}
	}

	public async ValueTask DisposeAsync()
	{
		await DisposeStaleConnectionAsync();
		_channelLock.Dispose();
	}
}
