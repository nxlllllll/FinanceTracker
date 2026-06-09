using System.Text;
using System.Text.Json;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
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

		_publisher = new RabbitMqPublisher(
			connectionFactory: new RabbitMqConnectionFactory(options: Options.Create(options: _options)),
			options: Options.Create(options: _options)
		);
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await _publisher.DisposeAsync();
		await _channel.DisposeAsync();
		await _connection.DisposeAsync();
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

	private static AggregateEventsMessage BuildMessage() => new AggregateEventsMessage(
		MessageId: Guid.CreateVersion7(),
		AggregateId: Guid.CreateVersion7(),
		AggregateType: AggregateTypeNames.Account,
		CorrelationId: Guid.CreateVersion7(),
		Events: []
	);
}
