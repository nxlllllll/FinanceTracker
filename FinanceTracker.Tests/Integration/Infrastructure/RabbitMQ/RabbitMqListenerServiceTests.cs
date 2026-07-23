using FinanceTracker.Contracts.Messages;
using System.Collections.Concurrent;
using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace FinanceTracker.Tests.Integration.Infrastructure.RabbitMQ;

public sealed class TestHandlerState
{
	private int _callCount;
	private readonly TaskCompletionSource<bool> _firstMessageTcs = new TaskCompletionSource<bool>(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
	private volatile TaskCompletionSource<bool>? _countTcs;
	private int _expectedCount;

	public int CallCount => _callCount;
	public AggregateEventsMessage? LastMessage { get; private set; }

	public Task<bool> WaitAsync(TimeSpan timeout)
		=> _firstMessageTcs.Task.WaitAsync(timeout: timeout).ContinueWith(continuationFunction: t => t is { IsCompletedSuccessfully: true, Result: true });

	public Task WaitForCountAsync(int expected, TimeSpan timeout)
	{
		_expectedCount = expected;
		_countTcs = new TaskCompletionSource<bool>(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
		return _countTcs.Task.WaitAsync(timeout: timeout);
	}

	public void Record(AggregateEventsMessage message)
	{
		LastMessage = message;
		int count = Interlocked.Increment(location: ref _callCount);
		_firstMessageTcs.TrySetResult(result: true);

		if (count >= _expectedCount)
			_countTcs?.TrySetResult(result: true);
	}
}

[RoutingKey(routingKey: AggregateTypeNames.Account)]
public sealed class TestMessageHandler(TestHandlerState state) : IMessageHandler<AggregateEventsMessage>
{
	public Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		state.Record(message: message);
		return Task.CompletedTask;
	}
}

[RoutingKey(routingKey: AggregateTypeNames.Account)]
public sealed class FailingMessageHandler(FailingMessageHandlerState state) : IMessageHandler<AggregateEventsMessage>
{
	public Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		state.Increment();
		throw new InvalidOperationException(message: "Simulated handler failure.");
	}
}

public sealed class FailingMessageHandlerState
{
	private int _callCount;
	public int CallCount => _callCount;
	public void Increment() => Interlocked.Increment(location: ref _callCount);
}

/// <summary>
/// Handler that fails the first <paramref name="failuresBeforeSuccess"/> invocations, then succeeds —
/// used to test the full "fail → delayed by the quorum queue's native backoff → redelivered → succeeds"
/// cycle, including the actual wall-clock gap between attempts.
/// </summary>
public sealed class FlakyMessageHandlerState(int failuresBeforeSuccess)
{
	private int _callCount;
	private readonly ConcurrentQueue<DateTimeOffset> _callTimestamps = new ConcurrentQueue<DateTimeOffset>();
	private readonly TaskCompletionSource _succeededTcs = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

	public int CallCount => _callCount;
	public IReadOnlyList<DateTimeOffset> CallTimestamps => [.. _callTimestamps.OrderBy(keySelector: t => t)];

	public Task<bool> WaitForSuccessAsync(TimeSpan timeout)
		=> _succeededTcs.Task.WaitAsync(timeout: timeout).ContinueWith(continuationFunction: t => t.IsCompletedSuccessfully);

	public void RecordCallAndMaybeThrow()
	{
		_callTimestamps.Enqueue(item: DateTimeOffset.UtcNow);
		int count = Interlocked.Increment(location: ref _callCount);

		if (count <= failuresBeforeSuccess)
			throw new InvalidOperationException(message: $"Simulated failure #{count}.");

		_succeededTcs.TrySetResult();
	}
}

[RoutingKey(routingKey: AggregateTypeNames.Account)]
public sealed class FlakyMessageHandler(FlakyMessageHandlerState state) : IMessageHandler<AggregateEventsMessage>
{
	public Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		state.RecordCallAndMaybeThrow();
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler that blocks indefinitely (never acks) until <see cref="Release"/> is called —
/// used to keep a delivered message "in flight" so tests can observe BasicQos prefetch
/// limits at the protocol level.
/// </summary>
public sealed class BlockingMessageHandlerState
{
	private int _callCount;
	private readonly TaskCompletionSource _firstMessageReceivedTcs = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource _releaseTcs = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

	public int CallCount => _callCount;

	public Task<bool> WaitForFirstMessageAsync(TimeSpan timeout)
		=> _firstMessageReceivedTcs.Task.WaitAsync(timeout: timeout).ContinueWith(continuationFunction: t => t.IsCompletedSuccessfully);

	public void Release() => _releaseTcs.TrySetResult();

	public async Task HandleAsync(CancellationToken ct)
	{
		Interlocked.Increment(location: ref _callCount);
		_firstMessageReceivedTcs.TrySetResult();
		await _releaseTcs.Task.WaitAsync(cancellationToken: ct);
	}
}

[RoutingKey(routingKey: AggregateTypeNames.Account)]
public sealed class BlockingMessageHandler(BlockingMessageHandlerState state) : IMessageHandler<AggregateEventsMessage>
{
	public Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
		=> state.HandleAsync(ct: ct);
}

public sealed class RabbitMqListenerServiceTests : RabbitMqDatabaseFixture
{
	private string _exchangeName = null!;
	private string _queueName = null!;
	private string _deadLetterExchangeName = null!;
	private string _deadLetterQueueName = null!;
	private IConnection _setupConnection = null!;
	private IChannel _setupChannel = null!;
	private RabbitMqOptions _baseOptions = null!;
	private RabbitMqConnectionFactory _connectionFactory = null!;

	[Before(hookType: Test)]
	public async Task SetupListenerAsync()
	{
		Uri uri = new Uri(uriString: RabbitMqConnectionString);

		_exchangeName = $"test.exchange.{Guid.CreateVersion7():N}";
		_queueName = $"test.queue.{Guid.CreateVersion7():N}";

		_deadLetterExchangeName = $"{_queueName}.dlx";
		_deadLetterQueueName = $"{_queueName}.dlq";

		_baseOptions = new RabbitMqOptions
		{
			Host = uri.Host,
			Port = uri.Port,
			Username = "guest",
			Password = "guest",
			ExchangeName = _exchangeName,
			QueueName = _queueName,
			MaxRetries = 3
		};

		(_setupConnection, _setupChannel) = await CreateChannelAsync();

		await _setupChannel.ExchangeDeclareAsync(
			exchange: _exchangeName,
			type: ExchangeType.Topic,
			durable: true,
			autoDelete: false
		);

		await _setupChannel.ExchangeDeclareAsync(
			exchange: _deadLetterExchangeName,
			type: ExchangeType.Fanout,
			durable: true,
			autoDelete: false
		);
		await _setupChannel.QueueDeclareAsync(
			queue: _deadLetterQueueName,
			durable: true,
			exclusive: false,
			autoDelete: false
		);
		await _setupChannel.QueueBindAsync(
			queue: _deadLetterQueueName,
			exchange: _deadLetterExchangeName,
			routingKey: String.Empty
		);

		_connectionFactory = new RabbitMqConnectionFactory(options: Options.Create(options: _baseOptions));
	}

	[After(hookType: Test)]
	public async Task TeardownListenerAsync()
	{
		await _setupChannel.DisposeAsync();
		await _setupConnection.DisposeAsync();
	}

	private RabbitMqListenerService<AggregateEventsMessage, THandler> BuildListener<THandler>(
		RabbitMqOptions options,
		IServiceScopeFactory scopeFactory)
		where THandler : class, IMessageHandler<AggregateEventsMessage>
	{
		return new RabbitMqListenerService<AggregateEventsMessage, THandler>(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: scopeFactory,
			logger: NullLogger<RabbitMqListenerService<AggregateEventsMessage, THandler>>.Instance
		);
	}

	private DeadLetterAuditListener<AggregateEventsMessage, THandler> BuildDeadLetterListener<THandler>(
		RabbitMqOptions options,
		IServiceScopeFactory scopeFactory)
		where THandler : class, IMessageHandler<AggregateEventsMessage>
	{
		return new DeadLetterAuditListener<AggregateEventsMessage, THandler>(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: scopeFactory,
			logger: NullLogger<DeadLetterAuditListener<AggregateEventsMessage, THandler>>.Instance
		);
	}

	private ServiceProvider BuildSuccessServiceProvider()
	{
		TestHandlerState state = new TestHandlerState();
		ServiceCollection services = new ServiceCollection();
		services.AddSingleton<TestHandlerState>(_ => state);
		services.AddScoped<TestMessageHandler>();
		return services.BuildServiceProvider();
	}

	private ServiceProvider BuildFailingServiceProvider(FailingMessageHandlerState handlerState)
	{
		ServiceCollection services = new ServiceCollection();
		services.AddSingleton<FailingMessageHandlerState>(implementationFactory: _ => handlerState);
		services.AddScoped<FailingMessageHandler>();
		services.AddScoped<FinanceTrackerContext>(implementationFactory: _ => CreateContext());
		services.AddScoped<IUnresolvableEventWriteRepository>(implementationFactory: sp =>
			new UnresolvableEventWriteRepository(context: sp.GetRequiredService<FinanceTrackerContext>())
		);
		services.AddScoped<IDateProvider>(implementationFactory: _ => FakeDateProvider.Default);
		services.AddScoped<IUnitOfWork>(implementationFactory: sp => new EFUnitOfWork(
			context: sp.GetRequiredService<FinanceTrackerContext>(),
			logger: NullLogger<EFUnitOfWork>.Instance
		));
		return services.BuildServiceProvider();
	}

	private ServiceProvider BuildBlockingServiceProvider()
	{
		BlockingMessageHandlerState state = new BlockingMessageHandlerState();
		ServiceCollection services = new ServiceCollection();
		services.AddSingleton<BlockingMessageHandlerState>(implementationFactory: _ => state);
		services.AddScoped<BlockingMessageHandler>();
		return services.BuildServiceProvider();
	}

	private async Task WaitForConsumerAsync()
	{
		DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(seconds: 10);
		while (DateTimeOffset.UtcNow < deadline)
		{
			try
			{
				await using IChannel probeChannel = await _setupConnection.CreateChannelAsync();
				QueueDeclareOk result = await probeChannel.QueueDeclarePassiveAsync(queue: _queueName);
				if (result.ConsumerCount > 0)
					return;
			}
			catch (OperationInterruptedException)
			{
				// Queue not declared yet by the listener's own ConnectAsync — expected during startup, retry.
			}
			await Task.Delay(millisecondsDelay: 100);
		}
		throw new TimeoutException(message: $"No consumer registered on '{_queueName}' within 10s.");
	}

	private static AggregateEventsMessage BuildMessage() => new AggregateEventsMessage(
		MessageId: Guid.CreateVersion7(),
		AggregateId: Guid.CreateVersion7(),
		AggregateType: AggregateTypeNames.Account,
		CorrelationId: Guid.CreateVersion7(),
		Events: []
	);

	private static byte[] BuildInvalidBody()
		=> [.. "{ not valid json @@@ }"u8];

	private async Task PublishRawAsync(byte[] body)
	{
		BasicProperties props = new BasicProperties
		{
			Persistent = true,
			ContentType = "application/json"
		};
		await _setupChannel.BasicPublishAsync(
			exchange: _exchangeName,
			routingKey: AggregateTypeNames.Account,
			mandatory: false,
			basicProperties: props,
			body: body
		);
	}

	[Test]
	public async Task Listener_WhenMessagePublished_ShouldCallHandler()
	{
		await using ServiceProvider sp = BuildSuccessServiceProvider();
		TestHandlerState state = sp.GetRequiredService<TestHandlerState>();
		RabbitMqListenerService<AggregateEventsMessage, TestMessageHandler> listener = BuildListener<TestMessageHandler>(
			options: _baseOptions,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: _baseOptions),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());
		bool received = await state.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: received).IsTrue();
	}

	[Test]
	public async Task Listener_WhenMessagePublished_ShouldCallHandlerExactlyOnce()
	{
		await using ServiceProvider sp = BuildSuccessServiceProvider();
		TestHandlerState state = sp.GetRequiredService<TestHandlerState>();
		RabbitMqListenerService<AggregateEventsMessage, TestMessageHandler> listener = BuildListener<TestMessageHandler>(
			options: _baseOptions,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: _baseOptions),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());
		await state.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));
		await Task.Delay(millisecondsDelay: 300);
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: state.CallCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Listener_WhenHandlerSucceeds_ShouldAckMessage()
	{
		await using ServiceProvider sp = BuildSuccessServiceProvider();
		RabbitMqListenerService<AggregateEventsMessage, TestMessageHandler> listener = BuildListener<TestMessageHandler>(
			options: _baseOptions,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		TestHandlerState state = sp.GetRequiredService<TestHandlerState>();
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: _baseOptions),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());
		await state.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));
		await Task.Delay(millisecondsDelay: 300);
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		QueueDeclareOk result = await _setupChannel.QueueDeclarePassiveAsync(queue: _queueName);
		await Assert.That(value: (int)result.MessageCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Listener_WhenMessageIsInvalidJson_ShouldDiscardWithoutCallingHandler()
	{
		await using ServiceProvider sp = BuildSuccessServiceProvider();
		TestHandlerState state = sp.GetRequiredService<TestHandlerState>();
		RabbitMqListenerService<AggregateEventsMessage, TestMessageHandler> listener = BuildListener<TestMessageHandler>(
			options: _baseOptions,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await PublishRawAsync(body: BuildInvalidBody());
		await Task.Delay(millisecondsDelay: 500);
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: state.CallCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Listener_WhenHandlerFails_ShouldNotRedeliverBeforeBackoffDelayElapses()
	{
		RabbitMqOptions options = _baseOptions with
		{
			MaxRetries = 5,
			DelayedRetryMinMs = 2000,
			DelayedRetryMaxMs = 2000
		};

		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		await WaitForConditionAsync(
			condition: () => Task.FromResult(result: handlerState.CallCount >= 1),
			timeout: TimeSpan.FromSeconds(seconds: 10)
		);

		await Task.Delay(millisecondsDelay: 500);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: handlerState.CallCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Listener_WhenHandlerFailsOnce_ShouldRedeliverAfterBackoffDelayAndSucceed()
	{
		RabbitMqOptions options = _baseOptions with
		{
			MaxRetries = 3,
			DelayedRetryMinMs = 1000,
			DelayedRetryMaxMs = 1000
		};

		FlakyMessageHandlerState handlerState = new FlakyMessageHandlerState(failuresBeforeSuccess: 1);
		ServiceCollection services = new ServiceCollection();
		services.AddSingleton<FlakyMessageHandlerState>(implementationFactory: _ => handlerState);
		services.AddScoped<FlakyMessageHandler>();
		await using ServiceProvider sp = services.BuildServiceProvider();

		RabbitMqListenerService<AggregateEventsMessage, FlakyMessageHandler> listener = BuildListener<FlakyMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		bool succeeded = await handlerState.WaitForSuccessAsync(timeout: TimeSpan.FromSeconds(seconds: 15));

		await Task.Delay(millisecondsDelay: 300);
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		IReadOnlyList<DateTimeOffset> timestamps = handlerState.CallTimestamps;

		await Assert.That(value: succeeded).IsTrue();
		await Assert.That(value: handlerState.CallCount).IsEqualTo(expected: 2);
		await Assert.That(value: timestamps.Count).IsEqualTo(expected: 2);

		TimeSpan elapsed = timestamps[1] - timestamps[0];
		await Assert.That(value: elapsed).IsGreaterThan(minimum: TimeSpan.FromMilliseconds(value: 700));

		QueueDeclareOk mainQueue = await _setupChannel.QueueDeclarePassiveAsync(queue: _queueName);
		await Assert.That(value: (int)mainQueue.MessageCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldEventuallyDeadLetter()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2, DelayedRetryMinMs = 200, DelayedRetryMaxMs = 200 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		BasicGetResult? deadLettered = null;
		await WaitForConditionAsync(
			condition: async () =>
			{
				deadLettered = await _setupChannel.BasicGetAsync(queue: _deadLetterQueueName, autoAck: true);
				return deadLettered is not null;
			},
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: deadLettered).IsNotNull();
		await Assert.That(value: handlerState.CallCount).IsGreaterThanOrEqualTo(minimum: options.MaxRetries);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldRecordUnresolvableEvent()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2, DelayedRetryMinMs = 200, DelayedRetryMaxMs = 200 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		DeadLetterAuditListener<AggregateEventsMessage, FailingMessageHandler> auditListener = BuildDeadLetterListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await auditListener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		await WaitForConditionAsync(
			condition: async () => await Context.UnresolvableEvents.AnyAsync(),
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();
		await auditListener.StopAsync(ct: CancellationToken.None);
		auditListener.Dispose();

		int count = await Context.UnresolvableEvents.CountAsync();
		await Assert.That(value: count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldRecordCorrectUnresolvableEventType()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2, DelayedRetryMinMs = 200, DelayedRetryMaxMs = 200 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		DeadLetterAuditListener<AggregateEventsMessage, FailingMessageHandler> auditListener = BuildDeadLetterListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await auditListener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		await WaitForConditionAsync(
			condition: async () => await Context.UnresolvableEvents.AnyAsync(),
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();
		await auditListener.StopAsync(ct: CancellationToken.None);
		auditListener.Dispose();

		UnresolvableEventEntity? entity = await Context.UnresolvableEvents.FirstOrDefaultAsync();
		await Assert.That(value: entity).IsNotNull();
		await Assert.That(value: entity!.Type).IsEqualTo(expected: UnresolvableEventType.ConsumerDeadLetter);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldRecordPayloadWithFullMessageBody()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2, DelayedRetryMinMs = 200, DelayedRetryMaxMs = 200 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		DeadLetterAuditListener<AggregateEventsMessage, FailingMessageHandler> auditListener = BuildDeadLetterListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		AggregateEventsMessage sent = BuildMessage();

		await listener.StartAsync(ct: CancellationToken.None);
		await auditListener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: sent);

		await WaitForConditionAsync(
			condition: async () => await Context.UnresolvableEvents.AnyAsync(),
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();
		await auditListener.StopAsync(ct: CancellationToken.None);
		auditListener.Dispose();

		UnresolvableEventEntity? entity = await Context.UnresolvableEvents.FirstOrDefaultAsync();
		await Assert.That(value: entity).IsNotNull();

		using JsonDocument doc = JsonDocument.Parse(json: entity!.Payload);
		JsonElement root = doc.RootElement;

		await Assert.That(value: root.GetProperty(propertyName: "messageType").GetString()).IsEqualTo(expected: nameof(AggregateEventsMessage));
		await Assert.That(value: root.GetProperty(propertyName: "deadLetterQueue").GetString()).IsEqualTo(expected: _deadLetterQueueName);

		string fullBody = root.GetProperty(propertyName: "body").GetString()!;
		AggregateEventsMessage? recovered = JsonSerializer.Deserialize<AggregateEventsMessage>(json: fullBody);
		await Assert.That(value: recovered).IsNotNull();
		await Assert.That(value: recovered!.MessageId).IsEqualTo(expected: sent.MessageId);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldRouteFullMessageToDeadLetterQueue()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2, DelayedRetryMinMs = 200, DelayedRetryMaxMs = 200 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		AggregateEventsMessage sent = BuildMessage();
		BasicGetResult? deadLettered = null;

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: sent);

		await WaitForConditionAsync(
			condition: async () =>
			{
				deadLettered = await _setupChannel.BasicGetAsync(queue: _deadLetterQueueName, autoAck: true);
				return deadLettered is not null;
			},
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		AggregateEventsMessage? recovered = JsonSerializer.Deserialize<AggregateEventsMessage>(utf8Json: deadLettered!.Body.ToArray());

		await Assert.That(value: recovered).IsNotNull();
		await Assert.That(value: recovered!.MessageId).IsEqualTo(expected: sent.MessageId);
		await Assert.That(value: recovered.AggregateId).IsEqualTo(expected: sent.AggregateId);
	}

	[Test]
	public async Task Listener_WithPrefetchCountOne_ShouldNotDeliverMoreThanOneUnackedMessage()
	{
		RabbitMqOptions options = _baseOptions with { PrefetchCount = 1 };

		await using ServiceProvider sp = BuildBlockingServiceProvider();
		BlockingMessageHandlerState state = sp.GetRequiredService<BlockingMessageHandlerState>();
		RabbitMqListenerService<AggregateEventsMessage, BlockingMessageHandler> listener = BuildListener<BlockingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();

		const int publishedCount = 5;
		for (int i = 0; i < publishedCount; i++)
			await publisher.PublishAsync(message: BuildMessage());

		bool firstReceived = await state.WaitForFirstMessageAsync(timeout: TimeSpan.FromSeconds(value: 5));

		await Task.Delay(millisecondsDelay: 300);

		QueueDeclareOk result = await _setupChannel.QueueDeclarePassiveAsync(queue: _queueName);

		state.Release();
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: firstReceived).IsTrue();
		await Assert.That(value: state.CallCount).IsEqualTo(expected: 1);
		await Assert.That(value: (int)result.MessageCount).IsEqualTo(expected: publishedCount - 1);
	}
}
