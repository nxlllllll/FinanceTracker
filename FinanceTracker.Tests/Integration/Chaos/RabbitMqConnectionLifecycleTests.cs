using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Tests.Integration.Chaos;

/// <summary>
/// Checks that reconnecting releases what it replaces, for every consumer in the host.
/// </summary>
public sealed class RabbitMqConnectionLifecycleTests
{
	private const int ForcedDropCount = 3;

	private RabbitMqContainer _rabbitMq = null!;
	private IHost _host = null!;

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		_rabbitMq = new RabbitMqBuilder(image: "rabbitmq:4.3.0")
			.WithUsername(username: "guest")
			.WithPassword(password: "guest")
			.Build();
		await _rabbitMq.StartAsync();

		_host = Host.CreateDefaultBuilder().ConfigureAppConfiguration(configureDelegate: (_, builder) => builder.AddInMemoryCollection(initialData: new Dictionary<string, string?>
		{
			["RabbitMQ:Host"] = _rabbitMq.Hostname,
			["RabbitMQ:Port"] = _rabbitMq.GetMappedPublicPort(containerPort: 5672).ToString(),
			["RabbitMQ:Username"] = "guest",
			["RabbitMQ:Password"] = "guest",
			["RabbitMQ:ExchangeName"] = "lifecycle-exchange",
			["RabbitMQ:QueueName"] = "lifecycle-queue",
			["RabbitMQ:MaxRetries"] = "3",
			["RabbitMQ:DelayedRetryMinMs"] = "1000",
			["RabbitMQ:DelayedRetryMaxMs"] = "5000",
			["RabbitMQ:PrefetchCount"] = "10",
			["RabbitMQ:MaxReconnectDelaySeconds"] = "1",
		})).ConfigureServices(configureDelegate: (_, services) =>
		{
			services.AddRabbitMqCore();
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

	private async Task CloseAllBrokerConnectionsAsync()
		=> await _rabbitMq.ExecAsync(command: ["rabbitmqctl", "close_all_connections", "lifecycle-test"]);

	private static async Task WaitForReconnectAsync(Dictionary<RabbitMqConsumerBase<ChaosTestMessage>, int> before)
	{
		DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(value: 30);

		while (DateTime.UtcNow < deadline)
		{
			if (before.All(predicate: pair => pair.Key.ConnectionsOpened > pair.Value))
				return;

			await Task.Delay(delay: TimeSpan.FromMilliseconds(value: 200));
		}

		string pending = String.Join(separator: ", ", values: before
			.Where(predicate: pair => pair.Key.ConnectionsOpened <= pair.Value)
			.Select(selector: pair => $"{pair.Key.GetType().Name} (still at {pair.Key.ConnectionsOpened})")
		);

		throw new TimeoutException(message: $"Not every consumer reconnected within 30s. Waiting on: {pending}.");
	}

	[Test]
	public async Task Consumers_AcrossRepeatedDrops_ShouldReleaseEveryConnectionTheyOpen()
	{
		IReadOnlyList<RabbitMqConsumerBase<ChaosTestMessage>> consumers = _host.Services
			.GetServices<IHostedService>()
			.OfType<RabbitMqConsumerBase<ChaosTestMessage>>()
			.ToList();

		await Assert.That(value: consumers).IsNotEmpty()
			.Because(message: "No consumers were resolved, so every comparison below would be between two zeros and the test would pass without checking anything.");

		foreach (RabbitMqConsumerBase<ChaosTestMessage> consumer in consumers)
		{
			await Assert.That(value: consumer.ConnectionsOpened).IsGreaterThan(minimum: 0)
				.Because(message: $"{consumer.GetType().Name} never connected, so the forced drops below would not exercise it.");
		}

		for (int drop = 0; drop < ForcedDropCount; drop++)
		{
			Dictionary<RabbitMqConsumerBase<ChaosTestMessage>, int> before = consumers.ToDictionary(
				keySelector: consumer => consumer,
				elementSelector: consumer => consumer.ConnectionsOpened
			);

			await CloseAllBrokerConnectionsAsync();
			await WaitForReconnectAsync(before: before);
		}

		foreach (RabbitMqConsumerBase<ChaosTestMessage> consumer in consumers)
		{
			await Assert.That(value: consumer.ConnectionsOpened).IsGreaterThanOrEqualTo(minimum: ForcedDropCount + 1)
				.Because(message: $"{consumer.GetType().Name} should have opened a fresh connection per forced drop. Fewer means the drops never reached it.");

			await Assert.That(value: consumer.ConnectionsReleased).IsEqualTo(expected: consumer.ConnectionsOpened - 1).Because(message: $"""
				{consumer.GetType().Name} has to release every connection it replaces. ConsumeAsync returns
				rather than throws when a connection drops, so releasing only in a catch block misses the
				ordinary path and leaves a socket the broker still counts as a live client — with automatic
				recovery off, nothing else will ever close it.
			""");
		}
	}
}
