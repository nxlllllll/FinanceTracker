using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Observability.Tracing;
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

namespace FinanceTracker.Worker.Shared.RabbitMQ.Publisher;

/// <summary>
/// Publishes <see cref="IRoutableMessage"/> instances to a RabbitMQ topic exchange.
/// Propagates the W3C <c>traceparent</c> header for distributed tracing across service boundaries.
/// </summary>
public sealed class RabbitMqPublisher : IRabbitMqPublisher
{
	private readonly RabbitMqConnectionFactory _connectionFactory;
	private readonly RabbitMqOptions _options;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<RabbitMqPublisher> _logger;

	private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(initialCount: 1, maxCount: 1);
	private readonly SemaphoreSlim _slots;
	private readonly ConcurrentBag<IChannel> _idleChannels = [];

	private IConnection? _connection;

	public RabbitMqPublisher(
		RabbitMqConnectionFactory connectionFactory,
		IOptions<RabbitMqOptions> options,
		IServiceScopeFactory scopeFactory,
		ILogger<RabbitMqPublisher> logger)
	{
		_connectionFactory = connectionFactory;
		_options = options.Value;
		_scopeFactory = scopeFactory;
		_logger = logger;
		_slots = new SemaphoreSlim(
			initialCount: _options.PublisherChannelPoolSize,
			maxCount: _options.PublisherChannelPoolSize
		);
	}

	public async Task PublishAsync<TMessage>(
		TMessage message,
		Guid? correlationId = default,
		CancellationToken ct = default
	) where TMessage : class, IRoutableMessage
	{
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

		await WithChannelAsync(action: async channel => await channel.BasicPublishAsync(
			exchange: _options.ExchangeName,
			routingKey: message.RoutingKey,
			mandatory: true,
			basicProperties: props,
			body: body,
			cancellationToken: ct
		), ct: ct);
	}

	/// <summary>
	/// Borrows a channel for the duration of <paramref name="action"/> and returns it afterwards.
	/// A channel is never handed to two callers at once: the slot is held for the whole call, and a
	/// borrowed channel is out of the bag until it comes back.
	/// </summary>
	private async Task WithChannelAsync(Func<IChannel, Task> action, CancellationToken ct)
	{
		await _slots.WaitAsync(cancellationToken: ct);

		IChannel? channel = null;

		try
		{
			channel = await RentChannelAsync(ct: ct);
			await action(channel);
		}
		finally
		{
			await ReturnChannelAsync(channel: channel);
			_slots.Release();
		}
	}

	/// <summary>
	/// Takes an open channel from the bag, discarding any that closed while idle, and opens a new one
	/// when none is available. The slot semaphore caps how many can exist.
	/// </summary>
	private async Task<IChannel> RentChannelAsync(CancellationToken ct)
	{
		while (_idleChannels.TryTake(result: out IChannel? pooled))
		{
			if (pooled is { IsOpen: true })
				return pooled;

			await DiscardChannelAsync(channel: pooled);
		}

		return await CreateChannelAsync(ct: ct);
	}

	/// <summary>
	/// Puts a still-open channel back for reuse, or disposes it. A channel that closed mid-publish is
	/// not worth keeping — the next caller would only find it closed and pay for the check.
	/// </summary>
	private async Task ReturnChannelAsync(IChannel? channel)
	{
		if (channel is null)
			return;

		if (channel.IsOpen)
		{
			_idleChannels.Add(item: channel);
			return;
		}

		await DiscardChannelAsync(channel: channel);
	}

	private async Task<IChannel> CreateChannelAsync(CancellationToken ct)
	{
		IConnection connection = await EnsureConnectionAsync(ct: ct);

		IChannel channel = await connection.CreateChannelAsync(
			options: new CreateChannelOptions(
				publisherConfirmationsEnabled: true,
				publisherConfirmationTrackingEnabled: true
			),
			cancellationToken: ct
		);

		channel.BasicReturnAsync += OnBasicReturnAsync;

		await channel.ExchangeDeclareAsync(
			exchange: _options.ExchangeName,
			type: ExchangeType.Topic,
			durable: true,
			cancellationToken: ct
		);

		return channel;
	}

	private async Task<IConnection> EnsureConnectionAsync(CancellationToken ct)
	{
		if (_connection is { IsOpen: true })
			return _connection;

		await _connectionLock.WaitAsync(cancellationToken: ct);

		try
		{
			if (_connection is { IsOpen: true })
				return _connection;

			await DiscardIdleChannelsAsync();
			await DisposeConnectionAsync();

			_connection = await _connectionFactory.CreateConnectionAsync(ct: ct);
			RabbitMqVersionGuard.EnsureSupportedVersion(connection: _connection);

			return _connection;
		}
		finally
		{
			_connectionLock.Release();
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
		_logger.ZLogError(message: $"""
			[RabbitMqPublisher] Message returned as unroutable: exchange='{args.Exchange}',
			routingKey='{args.RoutingKey}', replyCode={args.ReplyCode}, replyText='{args.ReplyText}'.
		""");

		await RecordUnroutableMessageAsync(args: args);
	}

	private async Task RecordUnroutableMessageAsync(BasicReturnEventArgs args)
	{
		try
		{
			await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

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
			_logger.ZLogError(exception: ex, message: $"[RabbitMqPublisher] Failed to record unroutable message in unresolvable_events.");
		}
	}

	private async Task DiscardChannelAsync(IChannel channel)
	{
		channel.BasicReturnAsync -= OnBasicReturnAsync;
		await channel.DisposeAsync();
	}

	private async Task DiscardIdleChannelsAsync()
	{
		while (_idleChannels.TryTake(result: out IChannel? channel))
			await DiscardChannelAsync(channel: channel);
	}

	private async Task DisposeConnectionAsync()
	{
		if (_connection is null)
			return;

		await _connection.DisposeAsync();
		_connection = null;
	}

	public async ValueTask DisposeAsync()
	{
		await DiscardIdleChannelsAsync();

		await DisposeConnectionAsync();

		_connectionLock.Dispose();
		_slots.Dispose();
	}
}
