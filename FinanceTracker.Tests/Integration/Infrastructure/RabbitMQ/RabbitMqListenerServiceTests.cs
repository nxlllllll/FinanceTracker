using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
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
using FinanceTracker.Worker.Shared.RabbitMQ.Retry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

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

public sealed class TestMessageHandler(TestHandlerState state) : IMessageHandler<AggregateEventsMessage>
{
	public Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		state.Record(message: message);
		return Task.CompletedTask;
	}
}

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
/// Handler that blocks indefinitely (never acks) until <see cref="Release"/> is called —
/// used to keep a delivered message "in flight" so tests can observe BasicQos prefetch
/// limits at the protocol level (the broker withholding further deliveries).
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

	public async Task HandleAsync()
	{
		Interlocked.Increment(location: ref _callCount);
		_firstMessageReceivedTcs.TrySetResult();
		await _releaseTcs.Task;
	}
}

public sealed class BlockingMessageHandler(BlockingMessageHandlerState state) : IMessageHandler<AggregateEventsMessage>
{
	public Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default) => state.HandleAsync();
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

		await _setupChannel.QueueDeclareAsync(
			queue: _queueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = _deadLetterExchangeName }
		);
		await _setupChannel.QueueBindAsync(
			queue: _queueName,
			exchange: _exchangeName,
			routingKey: AggregateTypeNames.Account
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
		IServiceScopeFactory scopeFactory,
		IRetryCounter? retryCounter = null)
		where THandler : class, IMessageHandler<AggregateEventsMessage>
	{
		return new RabbitMqListenerService<AggregateEventsMessage, THandler>(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: scopeFactory,
			retryCounter: retryCounter ?? new InMemoryRetryCounter(options: Options.Create(options: _baseOptions)),
			logger: NullLogger<RabbitMqListenerService<AggregateEventsMessage, THandler>>.Instance
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
			QueueDeclareOk result = await _setupChannel.QueueDeclarePassiveAsync(queue: _queueName);
			if (result.ConsumerCount > 0)
				return;
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
			options: Options.Create(options: _baseOptions)
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
			options: Options.Create(options: _baseOptions)
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
			options: Options.Create(options: _baseOptions)
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
	public async Task Listener_WhenHandlerFailsOnce_ShouldRetryAndEventuallySucceed()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 3 };

		int attemptCount = 0;
		ServiceCollection services = new ServiceCollection();
		services.AddSingleton<Func<int>>(implementationFactory: _ => () => Interlocked.Increment(location: ref attemptCount));
		await using ServiceProvider sp = services.BuildServiceProvider();

		RabbitMqListenerService<AggregateEventsMessage, TestMessageHandler> listener = BuildListener<TestMessageHandler>(
			options: options,
			scopeFactory: BuildSuccessServiceProvider().GetRequiredService<IServiceScopeFactory>()
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldNackWithoutRequeue()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options)
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		await WaitForConditionAsync(
			condition: () => Task.FromResult(result: handlerState.CallCount >= options.MaxRetries + 1),
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: handlerState.CallCount).IsEqualTo(expected: options.MaxRetries + 1);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldRecordUnresolvableEvent()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>() 
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options)
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		await WaitForConditionAsync(
			condition: async () => await Context.UnresolvableEvents.AnyAsync(),
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		int count = await Context.UnresolvableEvents.CountAsync();
		await Assert.That(value: count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldRecordCorrectUnresolvableEventType()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>() 
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options)
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		await WaitForConditionAsync(
			condition: async () => await Context.UnresolvableEvents.AnyAsync(),
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		UnresolvableEventEntity? entity = await Context.UnresolvableEvents.FirstOrDefaultAsync();
		await Assert.That(value: entity).IsNotNull();
		await Assert.That(value: entity!.Type).IsEqualTo(expected: UnresolvableEventType.ConsumerDeadLetter);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldRecordPayloadWithMessageMetadata()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>() 
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options)
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await publisher.PublishAsync(message: BuildMessage());

		await WaitForConditionAsync(
			condition: async () => await Context.UnresolvableEvents.AnyAsync(),
			timeout: TimeSpan.FromSeconds(seconds: 15)
		);

		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		UnresolvableEventEntity? entity = await Context.UnresolvableEvents.FirstOrDefaultAsync();
		await Assert.That(value: entity).IsNotNull();

		using JsonDocument doc = JsonDocument.Parse(json: entity!.Payload);
		JsonElement root = doc.RootElement;

		await Assert.That(value: root.GetProperty(propertyName: "messageType").GetString()).IsEqualTo(expected: nameof(AggregateEventsMessage));
		await Assert.That(value: root.GetProperty(propertyName: "queue").GetString()).IsEqualTo(expected: _queueName);
		await Assert.That(value: root.GetProperty(propertyName: "exceptionMessage").GetString()).IsEqualTo(expected: "Simulated handler failure.");
		await Assert.That(value: root.GetProperty(propertyName: "deadLetterQueue").GetString()).IsEqualTo(expected: _deadLetterQueueName);
	}

	[Test]
	public async Task Listener_WhenHandlerExceedsMaxRetries_ShouldRouteFullMessageToDeadLetterQueue()
	{
		RabbitMqOptions options = _baseOptions with { MaxRetries = 2 };
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: options,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>()
		);
		await using RabbitMqPublisher publisher = new RabbitMqPublisher(
			connectionFactory: _connectionFactory,
			options: Options.Create(options: options)
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
			options: Options.Create(options: options)
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