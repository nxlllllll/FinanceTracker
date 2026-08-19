using System.Runtime.ExceptionServices;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Utilities.Retry;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using ZLogger;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Handler;

/// <summary>
/// Owns the connection lifecycle shared by every consumer: connect, declare, consume, release,
/// wait, repeat. Subclasses describe what to declare and what to do with a delivery.
/// </summary>
public abstract class RabbitMqConsumerBase<TMessage>(
	RabbitMqConnectionFactory connectionFactory,
	ILogger logger
) : BackgroundService
	where TMessage : class
{
	private IConnection? _connection;
	private IChannel? _channel;
	public int ConnectionsOpened { get; private set; }
	public int ConnectionsReleased { get; private set; }
	protected IChannel Channel => _channel ?? throw new InvalidOperationException(
		message: $"{LogTag} has no open channel. Channel is only valid between ConnectAsync and the next release."
	);
	protected string LogTag => $"[{typeof(TMessage).Name}] {Description}";
	protected abstract string Description { get; }
	protected abstract string QueueName { get; }
	protected abstract int PrefetchCount { get; }
	protected abstract int MaxReconnectDelaySeconds { get; }

	protected abstract Task DeclareTopologyAsync(CancellationToken ct);

	protected abstract Task HandleDeliveryAsync(object sender, BasicDeliverEventArgs ea, CancellationToken ct);

	protected abstract Task OnDeliveryFailedAsync(BasicDeliverEventArgs ea, Exception exception);

	/// <summary>
	/// Reconnects on transient failures and stops the host on unrecoverable ones.
	/// </summary>
	protected override async Task ExecuteAsync(CancellationToken ct)
	{
		int attempt = 0;

		while (!ct.IsCancellationRequested)
		{
			ExceptionDispatchInfo? fatal = null;

			try
			{
				await ConnectAsync(ct: ct);

				attempt = 0;
				logger.ZLogInformation(message: $"{LogTag} connected successfully.");
				await ConsumeAsync(ct: ct);
				logger.ZLogInformation(message: $"{LogTag} lost its connection. Reconnecting.");
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception) when (IsUnrecoverable(exception: exception))
			{
				fatal = ExceptionDispatchInfo.Capture(source: Describe(exception: exception));
			}
			catch (Exception exception)
			{
				logger.ZLogError(exception: exception, message: $"{LogTag} connection attempt {attempt + 1} failed.");
			}
			finally
			{
				await ReleaseConnectionAsync();
			}

			if (fatal is not null)
			{
				logger.ZLogCritical(
					exception: fatal.SourceException,
					message: $"{LogTag} cannot start against this broker and will not retry. Stopping the host."
				);
				fatal.Throw();
			}

			if (ct.IsCancellationRequested)
				break;

			++attempt;
			int delaySeconds = RetryDelayCalculator.CalculateSeconds(attempt: attempt, maxSeconds: MaxReconnectDelaySeconds);

			logger.ZLogInformation(message: $"{LogTag} reconnecting in {delaySeconds}s (attempt {attempt}).");

			try
			{
				await Task.Delay(delay: TimeSpan.FromSeconds(value: delaySeconds), cancellationToken: ct);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	private static bool IsUnrecoverable(Exception exception) => Unwrap(exception: exception) switch
	{
		ConfigurationException => true,
		AuthenticationFailureException => true,
		OperationInterruptedException { ShutdownReason.ReplyCode: var replyCode } => IsFatalReplyCode(replyCode: replyCode),
		_ => false
	};

	private static bool IsFatalReplyCode(int replyCode)
		=> replyCode is Constants.AccessRefused or Constants.PreconditionFailed or Constants.NotAllowed;

	/// <summary>
	/// Finds the exception worth classifying inside a wrapper chain.
	/// </summary>
	private static Exception Unwrap(Exception exception)
	{
		for (Exception? current = exception; current is not null; current = current.InnerException)
		{
			if (current is ConfigurationException or AuthenticationFailureException or OperationInterruptedException)
				return current;
		}

		return exception;
	}

	private Exception Describe(Exception exception)
	{
		if (Unwrap(exception: exception) is not OperationInterruptedException interrupted || interrupted.ShutdownReason?.ReplyCode != Constants.PreconditionFailed)
			return exception;

		string brokerReply = interrupted.ShutdownReason.ReplyText;

		return new RabbitMqTopologyConflictException(
			message: $"""
				Queue '{QueueName}' already exists with arguments that differ from the ones this worker declares.
				The broker refused the declaration: {brokerReply}
				Arguments fixed at declaration time (x-delivery-limit from RabbitMQ:MaxRetries, x-delayed-retry-min/max
				from RabbitMQ:DelayedRetryMinMs/DelayedRetryMaxMs, x-queue-type, x-dead-letter-exchange) cannot be changed
				by re-declaring an existing queue. Either restore the previous values, or drain and delete the queue during
				a maintenance window, or move these settings to a RabbitMQ policy so they can be changed on a live broker.
				""",
			queueName: QueueName,
			brokerReply: brokerReply
		);
	}

	private async Task ConnectAsync(CancellationToken ct)
	{
		_connection = await connectionFactory.CreateConnectionAsync(ct: ct);
		ConnectionsOpened++;

		RabbitMqVersionGuard.EnsureSupportedVersion(connection: _connection);

		_channel = await _connection.CreateChannelAsync(cancellationToken: ct);

		await _channel.BasicQosAsync(
			prefetchSize: 0,
			prefetchCount: (ushort)PrefetchCount,
			global: false,
			cancellationToken: ct
		);

		await DeclareTopologyAsync(ct: ct);
	}

	private async Task ConsumeAsync(CancellationToken ct)
	{
		TaskCompletionSource connectionDropped = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

		_connection!.ConnectionShutdownAsync += (_, args) =>
		{
			logger.ZLogInformation(message: $"{LogTag} connection shutdown: {args.ReplyText}.");
			connectionDropped.TrySetResult();
			return Task.CompletedTask;
		};

		_channel!.ChannelShutdownAsync += (_, args) =>
		{
			logger.ZLogInformation(message: $"{LogTag} channel shutdown: {args.ReplyText}.");
			connectionDropped.TrySetResult();
			return Task.CompletedTask;
		};

		AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel!);
		consumer.ReceivedAsync += async (sender, ea) =>
		{
			try
			{
				await HandleDeliveryAsync(sender: sender, ea: ea, ct: ct);
			}
			catch (Exception exception)
			{
				logger.ZLogError(exception: exception, message: $"{LogTag} unhandled exception processing delivery {ea.DeliveryTag}.");
				await OnDeliveryFailedAsync(ea: ea, exception: exception);
			}
		};

		await _channel!.BasicConsumeAsync(
			queue: QueueName,
			autoAck: false,
			consumer: consumer,
			cancellationToken: ct
		);

		await using CancellationTokenRegistration registration = ct.Register(callback: () => connectionDropped.TrySetCanceled());
		await connectionDropped.Task;
	}

	/// <summary>
	/// Acknowledges without a cancellation token and swallows a closed channel. By the time an ack is
	/// due the work is already committed, so failing here would only turn a finished message into a
	/// redelivered one.
	/// </summary>
	protected async Task SafeAckAsync(ulong deliveryTag)
	{
		try
		{
			await Channel.BasicAckAsync(deliveryTag: deliveryTag, multiple: false, cancellationToken: CancellationToken.None);
		}
		catch (Exception ex) when (ex is AlreadyClosedException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
		{
			logger.ZLogWarning(exception: ex, message: $"{LogTag} ack failed for delivery {deliveryTag}: channel already closed.");
		}
	}

	/// <summary>
	/// See <see cref="SafeAckAsync"/> — same rationale, for the nack path. Used where a redelivery
	/// should not count against the queue's delivery limit.
	/// </summary>
	protected async Task SafeNackAsync(ulong deliveryTag, bool requeue)
	{
		try
		{
			await Channel.BasicNackAsync(deliveryTag: deliveryTag, multiple: false, requeue: requeue, cancellationToken: CancellationToken.None);
		}
		catch (Exception ex) when (ex is AlreadyClosedException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
		{
			logger.ZLogWarning(exception: ex, message: $"{LogTag} nack failed for delivery {deliveryTag}: channel already closed.");
		}
	}

	/// <summary>
	/// See <see cref="SafeAckAsync"/> — same rationale, for the reject path. Used for genuine handler
	/// failures, where consuming a delivery attempt is the point: after <c>x-delivery-limit</c>
	/// attempts the quorum queue dead-letters the message instead of retrying it forever.
	/// </summary>
	protected async Task SafeRejectAsync(ulong deliveryTag, bool requeue)
	{
		try
		{
			await Channel.BasicRejectAsync(deliveryTag: deliveryTag, requeue: requeue, cancellationToken: CancellationToken.None);
		}
		catch (Exception ex) when (ex is AlreadyClosedException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
		{
			logger.ZLogWarning(exception: ex, message: $"{LogTag} reject failed for delivery {deliveryTag}: channel already closed.");
		}
	}

	public override async Task StopAsync(CancellationToken ct)
	{
		await base.StopAsync(cancellationToken: ct);
		await ReleaseConnectionAsync();
		logger.ZLogInformation(message: $"{LogTag} stopped.");
	}

	private async Task ReleaseConnectionAsync()
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
			ConnectionsReleased++;
		}
	}
}
