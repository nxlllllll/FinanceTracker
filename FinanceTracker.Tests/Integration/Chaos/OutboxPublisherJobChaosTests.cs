using System.Text.Json;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Worker.Outbox.Job;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NSubstitute;
using Quartz;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Tests.Integration.Chaos;

/// <summary>
/// Verifies <see cref="OutboxPublisherJob"/>'s at-least-once delivery claim actually holds against a
/// real broker outage: a publish failure must not crash the job and must leave the message pending
/// with an incremented retry count (not silently dropped), and the message must actually get
/// published once the broker recovers.
/// </summary>
public sealed class OutboxPublisherJobChaosTests
{
	private const string TemplateDatabaseName = "ft_outbox_chaos_template";

	private PostgreSqlContainer _postgres = null!;
	private RabbitMqContainer _rabbitMq = null!;
	private IHost _host = null!;
	private string _databaseName = null!;

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		_postgres = new PostgreSqlBuilder(image: "postgres:16").WithCommand("-N", "500").Build();
		_rabbitMq = new RabbitMqBuilder(image: "rabbitmq:4.3.0")
			.WithUsername(username: "guest")
			.WithPassword(password: "guest")
			.WithPortBinding(hostPort: 25674, containerPort: 5672)
			.Build();
		await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

		string templateConnectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgres.GetConnectionString())
		{
			Database = TemplateDatabaseName
		}.ConnectionString;

		Migrator.DatabaseMigrator.Upgrade(connectionString: templateConnectionString, logToConsole: false);
		NpgsqlConnection.ClearPool(connection: new NpgsqlConnection(connectionString: templateConnectionString));

		_databaseName = $"ft_outbox_chaos_{Guid.CreateVersion7():N}";
		string connectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgres.GetConnectionString())
		{
			Database = _databaseName
		}.ConnectionString;

		string adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgres.GetConnectionString())
		{
			Database = "postgres"
		}.ConnectionString;

		await using (NpgsqlConnection adminConnection = new NpgsqlConnection(connectionString: adminConnectionString))
		{
			await adminConnection.OpenAsync();
			await using NpgsqlCommand command = new NpgsqlCommand(
				cmdText: $"CREATE DATABASE \"{_databaseName}\" TEMPLATE {TemplateDatabaseName}",
				connection: adminConnection
			);
			await command.ExecuteNonQueryAsync();
		}

		_host = Host.CreateDefaultBuilder().ConfigureAppConfiguration(configureDelegate: (_, builder) => builder.AddInMemoryCollection(initialData: new Dictionary<string, string?>
		{
			[$"ConnectionStrings:{nameof(FinanceTrackerContext)}"] = connectionString,
			["Redis:ConnectionString"] = "localhost:6379,abortConnect=false,connectTimeout=100",
			["Redis:InstanceName"] = "ft_outbox_chaos:",
			["RabbitMQ:Host"] = _rabbitMq.Hostname,
			["RabbitMQ:Port"] = _rabbitMq.GetMappedPublicPort(containerPort: 5672).ToString(),
			["RabbitMQ:Username"] = "guest",
			["RabbitMQ:Password"] = "guest",
			["RabbitMQ:ExchangeName"] = "outbox-chaos-exchange",
			["RabbitMQ:MaxRetries"] = "3",
			["RabbitMQ:DelayedRetryMinMs"] = "1000",
			["RabbitMQ:DelayedRetryMaxMs"] = "5000",
			["RabbitMQ:PrefetchCount"] = "10",
			["Outbox:IsEnabled"] = "true",
			["Outbox:IntervalSeconds"] = "3",
			["Outbox:BatchSize"] = "20",
			["Outbox:MaxRetries"] = "5",
			["Outbox:LeaseDurationSeconds"] = "60",
			["Outbox:Group"] = "chaos",
			["Outbox:TriggerName"] = "ChaosOutboxTrigger",
			["Retry:MaxRetries"] = "3",
			["Retry:BaseDelayMs"] = "5",
			["Retry:UseJitter"] = "false",
			["Idempotency:InFlightInitialDelayMs"] = "50",
			["Idempotency:InFlightMaxDelayMs"] = "500",
			["Idempotency:InFlightMaxWaitMs"] = "1000",
			["Idempotency:AbandonedAfterSeconds"] = "5",
			["RateLimit:RequestsPerWindow"] = "1000",
			["RateLimit:WindowSeconds"] = "60",
			["Argon2:Iterations"] = "2",
			["Argon2:MemorySize"] = "65536",
			["Argon2:DegreeOfParallelism"] = "1",
			["Jwt:Secret"] = "super-secret-test-key-at-least-32-chars!!",
			["Jwt:AccessTokenExpiryMinutes"] = "60",
			["Jwt:RefreshTokenExpiryDays"] = "7",
			["Jwt:Issuer"] = "test",
			["Jwt:Audience"] = "test",
		})).ConfigureServices(configureDelegate: (ctx, services) =>
		{
			services.AddPersistence(configuration: ctx.Configuration).AddAuth();
			services.AddRabbitMqCore();
			services.AddRabbitMqPublisher();

			services.AddOptions<OutboxOptions>()
				.BindConfiguration(configSectionPath: OutboxOptions.SectionName)
				.ValidateDataAnnotations()
				.ValidateOnStart();
			services.AddScoped<OutboxPublisherJob>();
		}).Build();

		await _host.StartAsync();

		await DeclareQueueBoundToRoutingKeyAsync();
	}

	private async Task DeclareQueueBoundToRoutingKeyAsync()
	{
		using IServiceScope scope = _host.Services.CreateScope();
		FinanceTracker.Worker.Shared.RabbitMQ.Connection.RabbitMqConnectionFactory connectionFactory =
			scope.ServiceProvider.GetRequiredService<FinanceTracker.Worker.Shared.RabbitMQ.Connection.RabbitMqConnectionFactory>();

		await using RabbitMQ.Client.IConnection connection = await connectionFactory.CreateConnectionAsync();
		await using RabbitMQ.Client.IChannel channel = await connection.CreateChannelAsync();

		await channel.ExchangeDeclareAsync(exchange: "outbox-chaos-exchange", type: RabbitMQ.Client.ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: CancellationToken.None);
		await channel.QueueDeclareAsync(queue: "outbox-chaos-sink", durable: true, exclusive: false, autoDelete: false, cancellationToken: CancellationToken.None);
		await channel.QueueBindAsync(queue: "outbox-chaos-sink", exchange: "outbox-chaos-exchange", routingKey: "ChaosTestAggregate", cancellationToken: CancellationToken.None);
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await _host.StopAsync();
		_host.Dispose();

		await _postgres.DisposeAsync();
		await _rabbitMq.DisposeAsync();
	}

	[Test]
	public async Task PublisherJob_WhenBrokerRecoversAfterOutage_ShouldPublishThePendingMessage()
	{
		Guid messageId = await SeedPendingOutboxMessageAsync();

		await _rabbitMq.StopAsync();

		await RunJobOnceAsync();

		(int retryCountDuringOutage, DateTimeOffset? processedAtDuringOutage, DateTimeOffset? failedAtDuringOutage) = await ReadOutboxRowAsync(messageId: messageId);

		await Assert.That(value: processedAtDuringOutage).IsNull()
			.Because(message: "A failed publish attempt must not mark the message as processed.");
		await Assert.That(value: failedAtDuringOutage).IsNull()
			.Because(message: "A single failed attempt is well under MaxRetries and should not dead-letter the message yet.");
		await Assert.That(value: retryCountDuringOutage).IsEqualTo(expected: 1)
			.Because(message: "A failed publish attempt must increment the retry count instead of silently dropping the message.");

		await _rabbitMq.StartAsync();
		await WaitForBrokerToAcceptConnectionsAsync();

		await RunJobOnceAsync();

		(int retryCountAfterRecovery, DateTimeOffset? processedAtAfterRecovery, DateTimeOffset? failedAtAfterRecovery) = await ReadOutboxRowAsync(messageId: messageId);

		await Assert.That(value: processedAtAfterRecovery).IsNotNull()
			.Because(message: "Once the broker is back, the next job run should successfully publish and mark the message as processed.");
		await Assert.That(value: failedAtAfterRecovery).IsNull();
		await Assert.That(value: retryCountAfterRecovery).IsEqualTo(expected: 1)
			.Because(message: "A successful publish should not touch the retry count further.");
	}

	private async Task<Guid> SeedPendingOutboxMessageAsync()
	{
		await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();
		FinanceTrackerContext context = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();

		Guid messageId = Guid.CreateVersion7();
		Guid aggregateId = Guid.CreateVersion7();
		Guid correlationId = Guid.CreateVersion7();

		string payload = JsonSerializer.Serialize(value: new OutboxPayload(
			AggregateId: aggregateId,
			CorrelationId: correlationId,
			Events: [new OutboxEventEnvelope(EventType: "chaos.test", EventPayload: "{}")]
		));

		await context.Database.ExecuteSqlInterpolatedAsync($"""
			INSERT INTO outbox_messages (id, aggregate_id, aggregate_type, payload)
			VALUES ({messageId}, {aggregateId}, 'ChaosTestAggregate', {payload}::jsonb)
		""");

		return messageId;
	}

	private async Task RunJobOnceAsync()
	{
		await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();
		OutboxPublisherJob job = scope.ServiceProvider.GetRequiredService<OutboxPublisherJob>();

		IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
		context.CancellationToken.Returns(returnThis: CancellationToken.None);

		await job.Execute(context: context);
	}

	private async Task<(int RetryCount, DateTimeOffset? ProcessedAt, DateTimeOffset? FailedAt)> ReadOutboxRowAsync(Guid messageId)
	{
		await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();
		FinanceTrackerContext context = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();

		OutboxRow row = await context.Database.SqlQuery<OutboxRow>($"""
			SELECT retry_count AS "RetryCount", processed_at AS "ProcessedAt", failed_at AS "FailedAt"
			FROM outbox_messages
			WHERE id = {messageId}
		""").SingleAsync();

		return (row.RetryCount, row.ProcessedAt, row.FailedAt);
	}

	private sealed record OutboxRow(int RetryCount, DateTimeOffset? ProcessedAt, DateTimeOffset? FailedAt);

	private async Task WaitForBrokerToAcceptConnectionsAsync()
	{
		await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();
		FinanceTracker.Worker.Shared.RabbitMQ.Connection.RabbitMqConnectionFactory connectionFactory =
			scope.ServiceProvider.GetRequiredService<FinanceTracker.Worker.Shared.RabbitMQ.Connection.RabbitMqConnectionFactory>();

		for (int attempt = 0; attempt < 15; attempt++)
		{
			try
			{
				await using RabbitMQ.Client.IConnection connection = await connectionFactory.CreateConnectionAsync();
				return;
			}
			catch
			{
				await Task.Delay(delay: TimeSpan.FromSeconds(value: 1));
			}
		}
	}
}
