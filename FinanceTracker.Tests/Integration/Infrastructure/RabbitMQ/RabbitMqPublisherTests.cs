using FinanceTracker.Contracts.Messages;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FinanceTracker.Tests.Integration.Infrastructure.RabbitMQ;

public sealed class RabbitMqPublisherTests : RabbitMqFixture
{
	private IConnection _connection = null!;
	private IChannel _channel = null!;
	private string _exchangeName = null!;
	private string _queueName = null!;
	private RabbitMqOptions _options = null!;
	private ServiceProvider _serviceProvider = null!;
	private RabbitMqPublisher _publisher = null!;

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		Uri uri = new Uri(uriString: ConnectionString);

		_exchangeName = $"test.exchange.{Guid.CreateVersion7():N}";
		_queueName = $"test.queue.{Guid.CreateVersion7():N}";

		_options = new RabbitMqOptions
		{
			Host = uri.Host,
			Port = uri.Port,
			Username = "guest",
			Password = "guest",
			ExchangeName = _exchangeName,
			QueueName = _queueName
		};

		(_connection, _channel) = await CreateChannelAsync();

		await _channel.ExchangeDeclareAsync(
			exchange: _exchangeName,
			type: ExchangeType.Topic,
			durable: true,
			autoDelete: false
		);

		await _channel.QueueDeclareAsync(
			queue: _queueName,
			durable: true,
			exclusive: false,
			autoDelete: false
		);

		await _channel.QueueBindAsync(
			queue: _queueName,
			exchange: _exchangeName,
			routingKey: AggregateTypeNames.Account
		);

		_serviceProvider = new ServiceCollection().BuildServiceProvider();

		_publisher = new RabbitMqPublisher(
			connectionFactory: new RabbitMqConnectionFactory(options: Options.Create(options: _options)),
			options: Options.Create(options: _options),
			scopeFactory: _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
			logger: NullLogger<RabbitMqPublisher>.Instance
		);
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await _publisher.DisposeAsync();
		await _channel.DisposeAsync();
		await _connection.DisposeAsync();
		await _serviceProvider.DisposeAsync();
	}

	[Test]
	public async Task PublishAsync_ShouldDeliverMessageToCorrectQueue()
	{
		AggregateEventsMessage message = BuildMessage();

		await _publisher.PublishAsync(message: message);

		await Task.Delay(millisecondsDelay: 200);

		QueueDeclareOk result = await _channel.QueueDeclarePassiveAsync(queue: _queueName);

		await Assert.That(value: (int)result.MessageCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task PublishAsync_ShouldDeliverMessageWithCorrelationId()
	{
		AggregateEventsMessage message = BuildMessage();
		Guid correlationId = Guid.CreateVersion7();
		string? receivedCorrelationId = null;

		TaskCompletionSource tcs = new TaskCompletionSource();
		AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel);
		consumer.ReceivedAsync += (_, ea) =>
		{
			receivedCorrelationId = ea.BasicProperties.CorrelationId;
			tcs.TrySetResult();
			return Task.CompletedTask;
		};

		await _channel.BasicConsumeAsync(queue: _queueName, autoAck: true, consumer: consumer);
		await _publisher.PublishAsync(message: message, correlationId: correlationId);

		await tcs.Task.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));

		await Assert.That(value: receivedCorrelationId).IsEqualTo(expected: correlationId.ToString());
	}

	[Test]
	public async Task PublishAsync_ShouldSerializeMessageCorrectly()
	{
		AggregateEventsMessage sent = BuildMessage();
		AggregateEventsMessage? received = null;

		TaskCompletionSource tcs = new TaskCompletionSource();
		AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel);
		consumer.ReceivedAsync += (_, ea) =>
		{
			received = JsonSerializer.Deserialize<AggregateEventsMessage>(
				json: Encoding.UTF8.GetString(bytes: ea.Body.ToArray()),
				options: FinanceTrackerJsonOptions.Payload
			);
			tcs.TrySetResult();
			return Task.CompletedTask;
		};

		await _channel.BasicConsumeAsync(queue: _queueName, autoAck: true, consumer: consumer);
		await _publisher.PublishAsync(message: sent);

		await tcs.Task.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));

		await Assert.That(value: received).IsNotNull();
		await Assert.That(value: received!.MessageId).IsEqualTo(expected: sent.MessageId);
		await Assert.That(value: received.AggregateId).IsEqualTo(expected: sent.AggregateId);
		await Assert.That(value: received.AggregateType).IsEqualTo(expected: sent.AggregateType);
	}

	[Test]
	public async Task PublishAsync_WhenNoCorrelationId_ShouldNotSetCorrelationIdProperty()
	{
		AggregateEventsMessage message = BuildMessage();
		string? receivedCorrelationId = null;

		TaskCompletionSource tcs = new TaskCompletionSource();
		AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel: _channel);
		consumer.ReceivedAsync += (_, ea) =>
		{
			receivedCorrelationId = ea.BasicProperties.CorrelationId;
			tcs.TrySetResult();
			return Task.CompletedTask;
		};

		await _channel.BasicConsumeAsync(queue: _queueName, autoAck: true, consumer: consumer);
		await _publisher.PublishAsync(message: message, correlationId: null);

		await tcs.Task.WaitAsync(timeout: TimeSpan.FromSeconds(value: 5));

		await Assert.That(value: receivedCorrelationId).IsNull();
	}

	[Test]
	public async Task PublishAsync_CalledTwiceSequentially_ShouldReuseSameChannel()
	{
		await _publisher.PublishAsync(message: BuildMessage());
		IChannel? channelAfterFirstCall = GetInternalChannel(publisher: _publisher);

		await _publisher.PublishAsync(message: BuildMessage());
		IChannel? channelAfterSecondCall = GetInternalChannel(publisher: _publisher);

		await Assert.That(value: channelAfterFirstCall).IsNotNull();
		await Assert.That(value: ReferenceEquals(objA: channelAfterFirstCall, objB: channelAfterSecondCall)).IsTrue();
	}

	[Test]
	public async Task PublishAsync_WhenCachedChannelIsClosed_ShouldTransparentlyReconnectAndDeliver()
	{
		await _publisher.PublishAsync(message: BuildMessage());
		IChannel? staleChannel = GetInternalChannel(publisher: _publisher);
		await Assert.That(value: staleChannel).IsNotNull();

		await staleChannel!.CloseAsync();
		await Assert.That(value: staleChannel.IsOpen).IsFalse();

		await _publisher.PublishAsync(message: BuildMessage());
		IChannel? freshChannel = GetInternalChannel(publisher: _publisher);

		await Assert.That(value: freshChannel).IsNotNull();
		await Assert.That(value: freshChannel!.IsOpen).IsTrue();
		await Assert.That(value: ReferenceEquals(objA: staleChannel, objB: freshChannel)).IsFalse();

		await Task.Delay(millisecondsDelay: 200);

		QueueDeclareOk result = await _channel.QueueDeclarePassiveAsync(queue: _queueName);

		await Assert.That(value: (int)result.MessageCount).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task PublishAsync_CalledConcurrentlyOnFreshPublisher_ShouldNotThrowAndShouldDeliverAllMessages()
	{
		const int concurrentPublishCount = 20;

		IEnumerable<Task> publishTasks = Enumerable.Range(start: 0, count: concurrentPublishCount).Select(
			selector: _ => _publisher.PublishAsync(message: BuildMessage())
		);

		await Task.WhenAll(tasks: publishTasks);

		await Task.Delay(millisecondsDelay: 300);

		QueueDeclareOk result = await _channel.QueueDeclarePassiveAsync(queue: _queueName);

		await Assert.That(value: (int)result.MessageCount).IsEqualTo(expected: concurrentPublishCount);
	}

	private static IChannel? GetInternalChannel(RabbitMqPublisher publisher)
	{
		FieldInfo? field = typeof(RabbitMqPublisher).GetField(
			name: "_channel",
			bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
		);

		return field?.GetValue(obj: publisher) as IChannel;
	}

	private static AggregateEventsMessage BuildMessage() => new AggregateEventsMessage(
		MessageId: Guid.CreateVersion7(),
		AggregateId: Guid.CreateVersion7(),
		AggregateType: AggregateTypeNames.Account,
		CorrelationId: Guid.CreateVersion7(),
		Events: []
	);
}
