using System.Collections.Concurrent;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Tests.Integration.Chaos;

/// <summary>
/// Verifies that <see cref="RabbitMqListenerService{TMessage,THandler}"/> actually reconnects and
/// resumes consuming after a broker outage — not just that the retry/backoff loop reads correctly,
/// but that it works end-to-end against a real broker that goes away and comes back.
/// </summary>
public sealed class RabbitMqOutageChaosTests
{
	private RabbitMqContainer _rabbitMq = null!;
	private IHost _host = null!;

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		ChaosTestMessageHandler.Received.Clear();

		_rabbitMq = new RabbitMqBuilder(image: "rabbitmq:4.3.0")
			.WithUsername(username: "guest")
			.WithPassword(password: "guest")
			.WithPortBinding(hostPort: 25673, containerPort: 5672)
			.Build();
		await _rabbitMq.StartAsync();

		_host = Host.CreateDefaultBuilder().ConfigureAppConfiguration(configureDelegate: (_, builder) => builder.AddInMemoryCollection(initialData: new Dictionary<string, string?>
		{
			["RabbitMQ:Host"] = _rabbitMq.Hostname,
			["RabbitMQ:Port"] = _rabbitMq.GetMappedPublicPort(containerPort: 5672).ToString(),
			["RabbitMQ:Username"] = "guest",
			["RabbitMQ:Password"] = "guest",
			["RabbitMQ:ExchangeName"] = "chaos-exchange",
			["RabbitMQ:QueueName"] = "chaos-queue",
			["RabbitMQ:MaxRetries"] = "3",
			["RabbitMQ:DelayedRetryMinMs"] = "1000",
			["RabbitMQ:DelayedRetryMaxMs"] = "5000",
			["RabbitMQ:PrefetchCount"] = "10",
		})).ConfigureServices(configureDelegate: (_, services) =>
		{
			services.AddRabbitMqCore();
			services.AddRabbitMqPublisher();
			services.AddRabbitMqListener<ChaosTestMessage, ChaosTestMessageHandler>();
		}).Build();

		await _host.StartAsync();

		await Task.Delay(delay: TimeSpan.FromSeconds(value: 2));
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await _host.StopAsync();
		_host.Dispose();
		await _rabbitMq.DisposeAsync();
	}

	[Test]
	public async Task Listener_AfterBrokerOutage_ShouldReconnectAndConsumeNewMessages()
	{
		await _rabbitMq.StopAsync();

		await Task.Delay(delay: TimeSpan.FromSeconds(value: 5));

		await _rabbitMq.StartAsync();

		IRabbitMqPublisher publisher = _host.Services.GetRequiredService<IRabbitMqPublisher>();
		Guid messageId = Guid.CreateVersion7();

		bool delivered = false;
		for (int attempt = 0; attempt < 15 && !delivered; attempt++)
		{
			await Task.Delay(delay: TimeSpan.FromSeconds(value: 2));

			try
			{
				await publisher.PublishAsync(message: new ChaosTestMessage(MessageId: messageId));
			}
			catch
			{
				continue;
			}

			delivered = await WaitForDeliveryAsync(messageId: messageId, timeout: TimeSpan.FromSeconds(value: 3));
		}

		await Assert.That(value: delivered).IsTrue().Because(
			message: "The listener should reconnect and resume consuming once the broker outage ends, instead of staying disconnected forever."
		);
	}

	private static async Task<bool> WaitForDeliveryAsync(Guid messageId, TimeSpan timeout)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (ChaosTestMessageHandler.Received.Contains(messageId))
				return true;

			await Task.Delay(delay: TimeSpan.FromMilliseconds(value: 100));
		}

		return false;
	}
}

[RoutingKey(routingKey: "chaos.test")]
public sealed record ChaosTestMessage(Guid MessageId) : IRoutableMessage
{
	public string RoutingKey => "chaos.test";
}

public sealed class ChaosTestMessageHandler : IMessageHandler<ChaosTestMessage>
{
	public static readonly ConcurrentBag<Guid> Received = [];

	public Task HandleAsync(ChaosTestMessage message, CancellationToken ct = default)
	{
		Received.Add(item: message.MessageId);
		return Task.CompletedTask;
	}
}
