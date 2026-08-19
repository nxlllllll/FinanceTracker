using System.Collections.Concurrent;
using DotNet.Testcontainers.Containers;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Tests.Integration.Chaos;

public sealed class RabbitMqFatalStartupChaosTests
{
	private sealed record StartupOutcome(bool Stopped, Exception? FatalException);

	private sealed class CapturingLoggerProvider : ILoggerProvider
	{
		public ConcurrentBag<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

		public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries: Entries);

		public void Dispose()
		{
		}

		private sealed class CapturingLogger(ConcurrentBag<(LogLevel Level, Exception? Exception)> entries) : ILogger
		{
			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(
				LogLevel logLevel,
				EventId eventId,
				TState state,
				Exception? exception,
				Func<TState, Exception?, string> formatter
			) => entries.Add(item: (logLevel, exception));
		}
	}

	private const string SupportedImage = "rabbitmq:4.3.0";
	private const string UnsupportedImage = "rabbitmq:3.13-alpine";
	private const string QueueName = "fatal-startup-queue";
	private const string ExchangeName = "fatal-startup-exchange";
	private const int ConfiguredDeliveryLimit = 3;
	private const int ConflictingDeliveryLimit = 9;

	private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(value: 20);
	private static readonly TimeSpan SurvivalWindow = TimeSpan.FromSeconds(value: 8);

	private RabbitMqContainer _rabbitMq = null!;

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		_rabbitMq = new RabbitMqBuilder(image: SupportedImage)
			.WithUsername(username: "guest")
			.WithPassword(password: "guest")
			.Build();

		await _rabbitMq.StartAsync();
	}

	[After(hookType: Test)]
	public async Task TeardownAsync() => await _rabbitMq.DisposeAsync();

	private async Task<StartupOutcome> RunUntilStopOrTimeoutAsync(
		RabbitMqContainer container,
		string username,
		string password,
		TimeSpan timeout)
	{
		using CapturingLoggerProvider logs = new CapturingLoggerProvider();

		IHost host = BuildHost(container: container, username: username, password: password, logs: logs);

		try
		{
			await host.StartAsync();

			bool stopped = await WaitForStoppingAsync(host: host, timeout: timeout);

			Exception? fatal = logs.Entries
				.Where(predicate: entry => entry.Level == LogLevel.Critical)
				.Select(selector: entry => entry.Exception)
				.FirstOrDefault(predicate: exception => exception is not null);

			return new StartupOutcome(Stopped: stopped, FatalException: fatal);
		}
		finally
		{
			await host.StopAsync();
			host.Dispose();
		}
	}

	private static IHost BuildHost(
		RabbitMqContainer container,
		string username,
		string password,
		ILoggerProvider logs
	) => Host.CreateDefaultBuilder().ConfigureAppConfiguration(configureDelegate: (_, builder) => builder.AddInMemoryCollection(initialData: new Dictionary<string, string?>
	{
		["RabbitMQ:Host"] = container.Hostname,
		["RabbitMQ:Port"] = container.GetMappedPublicPort(containerPort: 5672).ToString(),
		["RabbitMQ:Username"] = username,
		["RabbitMQ:Password"] = password,
		["RabbitMQ:ExchangeName"] = ExchangeName,
		["RabbitMQ:QueueName"] = QueueName,
		["RabbitMQ:MaxRetries"] = ConfiguredDeliveryLimit.ToString(),
		["RabbitMQ:DelayedRetryMinMs"] = "1000",
		["RabbitMQ:DelayedRetryMaxMs"] = "5000",
		["RabbitMQ:PrefetchCount"] = "10",
		["RabbitMQ:MaxReconnectDelaySeconds"] = "1"
	})).ConfigureLogging(configureLogging: builder => builder.AddProvider(provider: logs)).ConfigureServices(configureDelegate: (_, services) =>
	{
		services.AddRabbitMqCore();
		services.AddRabbitMqListener<ChaosTestMessage, ChaosTestMessageHandler>();
	}).Build();

	private static async Task<bool> WaitForStoppingAsync(IHost host, TimeSpan timeout)
	{
		IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

		TaskCompletionSource stopping = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

		await using CancellationTokenRegistration registration = lifetime.ApplicationStopping.Register(callback: () => stopping.TrySetResult());

		Task finished = await Task.WhenAny(stopping.Task, Task.Delay(delay: timeout));

		return finished == stopping.Task;
	}

	private async Task DeclareConflictingQueueAsync()
	{
		ConnectionFactory factory = new ConnectionFactory
		{
			HostName = _rabbitMq.Hostname,
			Port = _rabbitMq.GetMappedPublicPort(containerPort: 5672),
			UserName = "guest",
			Password = "guest"
		};

		await using IConnection connection = await factory.CreateConnectionAsync();
		await using IChannel channel = await connection.CreateChannelAsync();

		await channel.QueueDeclareAsync(
			queue: QueueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: new Dictionary<string, object?>
			{
				["x-queue-type"] = "quorum",
				["x-dead-letter-exchange"] = $"{QueueName}.dlx",
				["x-delivery-limit"] = ConflictingDeliveryLimit,
				["x-delayed-retry-type"] = "failed",
				["x-delayed-retry-min"] = 1000,
				["x-delayed-retry-max"] = 5000
			}
		);
	}

	private async Task AddUserAsync(string username, string password)
	{
		ExecResult result = await _rabbitMq.ExecAsync(command: ["rabbitmqctl", "add_user", username, password]);

		await Assert.That(value: result.ExitCode).IsEqualTo(expected: 0L).Because(message: $"""
			Creating '{username}' has to succeed, otherwise the worker below fails on rejected
			credentials and the test passes while proving nothing about the refusal it targets.
			stderr: {result.Stderr}
		""");
	}

	private async Task SetPermissionsAsync(string username, string pattern)
	{
		ExecResult result = await _rabbitMq.ExecAsync(command: ["rabbitmqctl", "set_permissions", "-p", "/", username, pattern, pattern, pattern]);

		await Assert.That(value: result.ExitCode).IsEqualTo(expected: 0L).Because(message: $"""
			Without permissions the user cannot open a connection at all, which is the vhost scenario
			covered by a different test. This one needs the connection to succeed and the declaration
			to be refused. stderr: {result.Stderr}
		""");
	}

	[Test]
	public async Task Listener_WhenTheBrokerMatchesTheConfiguration_ShouldKeepRunning()
	{
		StartupOutcome outcome = await RunUntilStopOrTimeoutAsync(
			container: _rabbitMq,
			username: "guest",
			password: "guest",
			timeout: SurvivalWindow
		);

		await Assert.That(value: outcome.Stopped).IsFalse().Because(message: """
			A healthy broker must leave the host running. Every other test in this class asserts the
			opposite outcome, so if the host stops here they would all pass without proving anything
			about how unrecoverable failures are classified.
		""");
	}

	[Test]
	public async Task Listener_WhenQueueExistsWithDifferentArguments_ShouldStopHost()
	{
		await DeclareConflictingQueueAsync();

		StartupOutcome outcome = await RunUntilStopOrTimeoutAsync(
			container: _rabbitMq,
			username: "guest",
			password: "guest",
			timeout: StopTimeout
		);

		await Assert.That(value: outcome.Stopped).IsTrue().Because(message: """
			x-delivery-limit is fixed when a queue is declared, so a worker configured with a different
			MaxRetries can never make its declaration succeed against this queue. Staying up means
			retrying the same rejected declaration until someone notices the queue has no consumers.
		""");

		await Assert.That(value: outcome.FatalException).IsTypeOf<RabbitMqTopologyConflictException>().Because(message: """
			The broker's own 406 reply names the offending argument but not the setting behind it. The
			operator reading this log line is the one who has to choose between reverting MaxRetries and
			recreating the queue, and cannot do that from the raw reply text alone.
		""");

		await Assert.That(value: outcome.FatalException!.Message).Contains(expected: QueueName).Because(message: """
			A worker hosting several listeners produces this failure for one queue and not the others,
			so the message has to say which one.
		""");
	}

	[Test]
	public async Task Listener_WhenDeclaringTheQueueIsForbidden_ShouldStopHost()
	{
		await AddUserAsync(username: "limited", password: "limited-pass");
		await SetPermissionsAsync(username: "limited", pattern: "^allowed.*");

		StartupOutcome outcome = await RunUntilStopOrTimeoutAsync(
			container: _rabbitMq,
			username: "limited",
			password: "limited-pass",
			timeout: StopTimeout
		);

		await Assert.That(value: outcome.Stopped).IsTrue().Because(message: """
			The connection opens because the user has some permission on the vhost, and the refusal
			lands on the channel when the topology is declared. Nothing this worker can send afterwards
			changes the answer.
		""");

		await Assert.That(value: outcome.FatalException).IsNotNull().Because(message: """
			Without an exception on the record the host could have stopped for an unrelated reason and
			this assertion would not distinguish the two.
		""");
	}

	[Test]
	public async Task Listener_WhenTheUserHasNoAccessToTheVhost_ShouldStopHost()
	{
		await AddUserAsync(username: "outsider", password: "outsider-pass");

		StartupOutcome outcome = await RunUntilStopOrTimeoutAsync(
			container: _rabbitMq,
			username: "outsider",
			password: "outsider-pass",
			timeout: StopTimeout
		);

		await Assert.That(value: outcome.Stopped).IsTrue().Because(message: """
			This refusal arrives while the connection is being established, so ConnectionFactory wraps
			it in BrokerUnreachableException and the real cause sits in InnerException. Classifying only
			the outer type reads this as an unreachable broker and reconnects forever.
		""");

		await Assert.That(value: outcome.FatalException).IsNotNull().Because(message: """
			Without an exception on the record the host could have stopped for an unrelated reason and
			this assertion would not distinguish the two.
		""");
	}

	[Test]
	public async Task Listener_WhenCredentialsAreRejected_ShouldStopHost()
	{
		StartupOutcome outcome = await RunUntilStopOrTimeoutAsync(
			container: _rabbitMq,
			username: "guest",
			password: "not-the-password",
			timeout: StopTimeout
		);

		await Assert.That(value: outcome.Stopped).IsTrue().Because(message: """
			A rejected password carries no shutdown reply code at all, so a classifier that only reads
			reply codes treats it as transient. Nothing on this side will ever produce a different
			password.
		""");

		await Assert.That(value: outcome.FatalException).IsNotNull().Because(message: """
			Without an exception on the record the host could have stopped for an unrelated reason and
			this assertion would not distinguish the two.
		""");
	}

	[Test]
	public async Task Listener_WhenBrokerVersionIsUnsupported_ShouldStopHost()
	{
		await using RabbitMqContainer legacyRabbitMq = new RabbitMqBuilder(image: UnsupportedImage)
			.WithUsername(username: "guest")
			.WithPassword(password: "guest")
			.Build();

		await legacyRabbitMq.StartAsync();

		StartupOutcome outcome = await RunUntilStopOrTimeoutAsync(
			container: legacyRabbitMq,
			username: "guest",
			password: "guest",
			timeout: StopTimeout
		);

		await Assert.That(value: outcome.Stopped).IsTrue().Because(message: """
			RabbitMqVersionGuard exists to refuse a broker that silently ignores the x-delayed-retry-*
			arguments, which would disable retry backoff without any visible symptom. Its exception was
			raised from the connect path and swallowed by the same reconnect loop, so the guard detected
			the incompatibility and then did nothing about it.
		""");

		await Assert.That(value: outcome.FatalException).IsTypeOf<UnsupportedRabbitMqVersionException>().Because(message: """
			Any other exception type here means the run failed before reaching the guard — most likely
			the image no longer starts — and the guard itself was never exercised.
		""");
	}
}
