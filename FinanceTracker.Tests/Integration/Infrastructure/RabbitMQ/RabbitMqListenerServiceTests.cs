using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
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

public sealed class RabbitMqListenerServiceTests : RabbitMqDatabaseFixture
{
	private string _exchangeName = null!;
	private string _queueName = null!;
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
		await _setupChannel.QueueDeclareAsync(
			queue: _queueName,
			durable: true,
			exclusive: false,
			autoDelete: false
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
		services.AddScoped<IUnresolvableEventWriteRepository>(implementationFactory: _ => new UnresolvableEventWriteRepository(context: Context));
		services.AddScoped<IDateProvider>(implementationFactory: _ => FakeDateProvider.Default);
		services.AddScoped<IUnitOfWork>(implementationFactory: _ => new EFUnitOfWork(
			context: Context,
			logger: NullLogger<EFUnitOfWork>.Instance)
		);
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
	public async Task Listener_WhenMessagePublished_ShouldDeserializeMessageCorrectly()
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
		AggregateEventsMessage sent = BuildMessage();
		await publisher.PublishAsync(message: sent);
		await state.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: state.LastMessage).IsNotNull();
		await Assert.That(value: state.LastMessage!.MessageId).IsEqualTo(expected: sent.MessageId);
		await Assert.That(value: state.LastMessage.AggregateType).IsEqualTo(expected: sent.AggregateType);
		await Assert.That(value: state.LastMessage.AggregateId).IsEqualTo(expected: sent.AggregateId);
	}

	[Test]
	public async Task Listener_WhenMultipleMessagesPublished_ShouldHandleAll()
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
		await publisher.PublishAsync(message: BuildMessage());
		await publisher.PublishAsync(message: BuildMessage());
		await state.WaitForCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(value: 5));
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		await Assert.That(value: state.CallCount).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task Listener_WhenDeserializationFails_ShouldDiscardMessage()
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
	public async Task Listener_WhenDeserializationFails_ShouldNotRecordUnresolvableEvent()
	{
		FailingMessageHandlerState handlerState = new FailingMessageHandlerState();
		await using ServiceProvider sp = BuildFailingServiceProvider(handlerState: handlerState);
		RabbitMqListenerService<AggregateEventsMessage, FailingMessageHandler> listener = BuildListener<FailingMessageHandler>(
			options: _baseOptions,
			scopeFactory: sp.GetRequiredService<IServiceScopeFactory>() 
		);

		await listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();
		await PublishRawAsync(body: BuildInvalidBody());
		await Task.Delay(millisecondsDelay: 500);
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		int count = await Context.UnresolvableEvents.CountAsync();
		await Assert.That(value: count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Listener_WhenHandlerFailsRepeatedly_ShouldRequeueUntilMaxRetries()
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
		await Task.Delay(millisecondsDelay: 3000);
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
		await Task.Delay(millisecondsDelay: 3000);
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
		await Task.Delay(millisecondsDelay: 3000);
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
		await Task.Delay(millisecondsDelay: 3000);
		await listener.StopAsync(ct: CancellationToken.None);
		listener.Dispose();

		UnresolvableEventEntity? entity = await Context.UnresolvableEvents.FirstOrDefaultAsync();
		await Assert.That(value: entity).IsNotNull();

		using JsonDocument doc = JsonDocument.Parse(json: entity!.Payload);
		JsonElement root = doc.RootElement;

		await Assert.That(value: root.GetProperty(propertyName: "messageType").GetString()).IsEqualTo(expected: nameof(AggregateEventsMessage));
		await Assert.That(value: root.GetProperty(propertyName: "queue").GetString()).IsEqualTo(expected: _queueName);
		await Assert.That(value: root.GetProperty(propertyName: "exceptionMessage").GetString()).IsEqualTo(expected: "Simulated handler failure.");
	}
}