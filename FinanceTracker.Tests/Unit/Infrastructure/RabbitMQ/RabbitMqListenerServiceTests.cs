using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinanceTracker.Tests.Unit.Infrastructure.RabbitMQ;

public sealed class TestHandlerState
{
	private int _callCount;
	private AggregateEventsMessage? _lastMessage;
	private readonly TaskCompletionSource<bool> _firstMessageTcs = new TaskCompletionSource<bool>(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
	private volatile TaskCompletionSource<bool>? _countTcs;
	private int _expectedCount;

	public int CallCount => _callCount;
	public AggregateEventsMessage? LastMessage => _lastMessage;

	public Task<bool> WaitAsync(TimeSpan timeout)
	{
		return _firstMessageTcs.Task.WaitAsync(timeout: timeout).ContinueWith(continuationFunction: t => t is
		{
			IsCompletedSuccessfully: true,
			Result: true
		});
	}

	public Task WaitForCountAsync(int expected, TimeSpan timeout)
	{
		_expectedCount = expected;
		_countTcs = new TaskCompletionSource<bool>(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
		return _countTcs.Task.WaitAsync(timeout: timeout);
	}

	public void Record(AggregateEventsMessage message)
	{
		_lastMessage = message;
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

public sealed class RabbitMqListenerServiceTests : RabbitMqFixture
{
	private string _exchangeName = null!;
	private string _queueName = null!;
	private IConnection _setupConnection = null!;
	private IChannel _setupChannel = null!;
	private RabbitMqPublisher _publisher = null!;
	private RabbitMqListenerService<AggregateEventsMessage, TestMessageHandler, Account> _listener = null!;
	private ServiceProvider _serviceProvider = null!;
	private TestHandlerState _handlerState = null!;

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		Uri uri = new Uri(uriString: ConnectionString);

		_exchangeName = $"test.exchange.{Guid.CreateVersion7():N}";
		_queueName = $"test.queue.{Guid.CreateVersion7():N}";

		RabbitMqOptions options = new RabbitMqOptions
		{
			Host = uri.Host,
			Port = uri.Port,
			Username = "guest",
			Password = "guest",
			ExchangeName = _exchangeName,
			QueueName = _queueName
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

		RabbitMqConnectionFactory connectionFactory = new RabbitMqConnectionFactory(options: Options.Create(options: options));

		_handlerState = new TestHandlerState();

		ServiceCollection services = new ServiceCollection();
		services.AddSingleton<TestHandlerState>(_ => _handlerState);
		services.AddScoped<TestMessageHandler>();
		_serviceProvider = services.BuildServiceProvider();

		_publisher = new RabbitMqPublisher(
			connectionFactory: connectionFactory,
			options: Options.Create(options: options)
		);

		_listener = new RabbitMqListenerService<AggregateEventsMessage, TestMessageHandler, Account>(
			connectionFactory: connectionFactory,
			options: Options.Create(options: options),
			scopeFactory: _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqListenerService<AggregateEventsMessage, TestMessageHandler, Account>>.Instance
		);
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await _listener.StopAsync(ct: CancellationToken.None);
		_listener.Dispose();
		await _publisher.DisposeAsync();
		await _setupChannel.DisposeAsync();
		await _setupConnection.DisposeAsync();
		await _serviceProvider.DisposeAsync();
	}

	[Test]
	public async Task Listener_WhenMessagePublished_ShouldCallHandler()
	{
		await _listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();

		await _publisher.PublishAsync(message: BuildMessage());

		bool received = await _handlerState.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));

		await Assert.That(value: received).IsTrue();
	}

	[Test]
	public async Task Listener_WhenMessagePublished_ShouldCallHandlerExactlyOnce()
	{
		await _listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();

		await _publisher.PublishAsync(message: BuildMessage());

		await _handlerState.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));
		await Task.Delay(millisecondsDelay: 300);

		await Assert.That(value: _handlerState.CallCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Listener_WhenMessagePublished_ShouldDeserializeMessageCorrectly()
	{
		await _listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();

		AggregateEventsMessage sent = BuildMessage();
		await _publisher.PublishAsync(message: sent);

		await _handlerState.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));

		AggregateEventsMessage? received = _handlerState.LastMessage;

		await Assert.That(value: received).IsNotNull();
		await Assert.That(value: received!.MessageId).IsEqualTo(expected: sent.MessageId);
		await Assert.That(value: received.AggregateType).IsEqualTo(expected: sent.AggregateType);
		await Assert.That(value: received.AggregateId).IsEqualTo(expected: sent.AggregateId);
	}

	[Test]
	public async Task Listener_WhenMultipleMessagesPublished_ShouldHandleAll()
	{
		await _listener.StartAsync(ct: CancellationToken.None);
		await WaitForConsumerAsync();

		await _publisher.PublishAsync(message: BuildMessage());
		await _publisher.PublishAsync(message: BuildMessage());
		await _publisher.PublishAsync(message: BuildMessage());

		await _handlerState.WaitForCountAsync(expected: 3, timeout: TimeSpan.FromSeconds(value: 5));

		await Assert.That(value: _handlerState.CallCount).IsEqualTo(expected: 3);
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
}
