using FinanceTracker.Application.Configurations;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Services.TransferCompensation;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Worker.AccountProjection.Consumer;
using FinanceTracker.Worker.AccountProjection.Projection;
using FinanceTracker.Worker.BalanceAdjustment.Job;
using FinanceTracker.Worker.Outbox.Job;
using FinanceTracker.Worker.RecurringTransaction.Job;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumer;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.TransferProjection.Consumer;
using FinanceTracker.Worker.TransferProjection.Job;
using FinanceTracker.Worker.TransferProjection.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NSubstitute;
using Quartz;
using RabbitMQ.Client;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace FinanceTracker.Tests.Integration._Shared.Fixtures;

/// <summary>
/// Full E2E fixture: real Postgres + Redis + RabbitMQ.
/// RabbitMQ listeners (AccountEventsConsumer, AccountTransferConsumer,
/// RecurringTransactionConsumer) start as BackgroundServices with the Host.
/// Jobs are called directly via a NSubstitute IJobExecutionContext mock.
/// Use WaitForConditionAsync to await eventual consumer processing.
/// </summary>
public abstract class E2EFixture
{
	private const string TemplateDatabaseName = "ft_template";

	private static PostgreSqlContainer _postgres = null!;
	private static RedisContainer _redis = null!;
	private static RabbitMqContainer _rabbitMq = null!;

	protected IHost Host = null!;
	private string _connectionString = null!;
	private const string RabbitData = "guest";
	private string _testRunId = null!;
	private Uri _rabbitUri = null!;

	protected IMediator Mediator { get; private set; } = null!;
	protected FinanceTrackerContext Context { get; private set; } = null!;

	private static string AccountQueueName(string testRunId) => $"ft-e2e-account-{testRunId}";
	private static string TransferQueueName(string testRunId) => $"ft-e2e-transfer-{testRunId}";
	private static string RecurringQueueName(string testRunId) => $"ft-e2e-recurring-{testRunId}";
	private static string ExchangeName(string testRunId) => $"ft-e2e-{testRunId}";

	[Before(hookType: Assembly)]
	public static async Task StartContainersAsync()
	{
		Task postgres = Task.Run(async () =>
		{
			_postgres = new PostgreSqlBuilder(image: "postgres:16").WithCommand("-N", "700").Build();
			await _postgres.StartAsync();

			string templateConnectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgres.GetConnectionString())
			{
				Database = TemplateDatabaseName
			}.ConnectionString;

			Migrator.DatabaseMigrator.Upgrade(connectionString: templateConnectionString, logToConsole: false);
			NpgsqlConnection.ClearPool(connection: new NpgsqlConnection(connectionString: templateConnectionString));
		});

		Task redis = Task.Run(async () =>
		{
			_redis = new RedisBuilder(image: "redis:7").Build();
			await _redis.StartAsync();
		});

		Task rabbitMq = Task.Run(async () =>
		{
			_rabbitMq = new RabbitMqBuilder(image: "rabbitmq:4.3.0")
				.WithUsername(username: RabbitData)
				.WithPassword(password: RabbitData)
				.Build();
			await _rabbitMq.StartAsync();
		});

		await Task.WhenAll(postgres, redis, rabbitMq);
	}

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		string testRunId = Guid.CreateVersion7().ToString(format: "N");
		_testRunId = testRunId;

		_connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
		{
			Database = $"ft_e2e_{testRunId}"
		}.ConnectionString;

		Uri rabbitUri = new Uri(_rabbitMq.GetConnectionString());
		_rabbitUri = rabbitUri;
		string redisCs = _redis.GetConnectionString() + ",allowAdmin=true";

		await DeclareRabbitTopologyAsync(rabbitUri: rabbitUri, testRunId: testRunId);

		string adminConnectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
		{
			Database = "postgres"
		}.ConnectionString;

		await using (NpgsqlConnection adminConnection = new NpgsqlConnection(connectionString: adminConnectionString))
		{
			await adminConnection.OpenAsync();
			await using NpgsqlCommand command = new NpgsqlCommand(
				cmdText: $"CREATE DATABASE \"ft_e2e_{testRunId}\" TEMPLATE {TemplateDatabaseName}",
				connection: adminConnection
			);
			await command.ExecuteNonQueryAsync();
		}

		Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
			.ConfigureAppConfiguration(configureDelegate: (_, b) => b.AddInMemoryCollection(initialData: new Dictionary<string, string?>
			{
				[$"ConnectionStrings:{nameof(FinanceTrackerContext)}"] = _connectionString,
				[$"{RedisOptions.SectionName}:ConnectionString"] = redisCs,
				[$"{RedisOptions.SectionName}:InstanceName"] = $"ft_e2e_{testRunId}:",
				[$"{EventStoreOptions.SectionName}:SnapshotThreshold"] = "25",
				["Retry:MaxRetries"] = "3",
				["Retry:BaseDelayMs"] = "5",
				["Retry:UseJitter"] = "false",
				["Idempotency:InFlightInitialDelayMs"] = "50",
				["Idempotency:InFlightMaxDelayMs"] = "200",
				["Idempotency:InFlightMaxWaitMs"] = "500",
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
				["ProjectionRetry:MaxRetries"] = "3",
				["ProjectionRetry:BaseDelayMs"] = "5",
				["ProjectionRetry:UseJitter"] = "false",
				["RabbitMQ:Host"] = rabbitUri.Host,
				["RabbitMQ:Port"] = rabbitUri.Port.ToString(),
				["RabbitMQ:Username"] = RabbitData,
				["RabbitMQ:Password"] = RabbitData,
				["RabbitMQ:ExchangeName"] = ExchangeName(testRunId: testRunId),
				["RabbitMQ:QueueName"] = "ft-e2e",
				["RabbitMQ:QueueNameOverrides:AccountEventsConsumer"] = AccountQueueName(testRunId: testRunId),
				["RabbitMQ:QueueNameOverrides:AccountTransferConsumer"] = TransferQueueName(testRunId: testRunId),
				["RabbitMQ:QueueNameOverrides:RecurringTransactionConsumer"] = RecurringQueueName(testRunId: testRunId),
				["RabbitMQ:MaxRetries"] = "3",
				["Outbox:BatchSize"] = "50",
				["Outbox:MaxRetries"] = "3",
				["Outbox:Group"] = "test",
				["Outbox:TriggerName"] = "OutboxTrigger",
				["BalanceAdjustmentJob:CronExpression"] = "0 0 3 * * ?",
				["BalanceAdjustmentJob:Group"] = "test",
				["BalanceAdjustmentJob:TriggerName"] = "BalanceTrigger",
				["BalanceAdjustmentJob:MaxRetries"] = "3",
				["BalanceAdjustmentJob:BaseDelayMs"] = "5",
				["BalanceAdjustmentJob:UseJitter"] = "false",
				["TransferCreditLag:GracePeriodMinutes"] = "5",
				["TransferCreditLag:CompensationThresholdMinutes"] = "30",
				["TransferCreditLag:Group"] = "test",
				["TransferCreditLag:TriggerName"] = "TransferLagTrigger",
				["RecurringTransaction:CronExpression"] = "0 0 3 * * ?",
				["RecurringTransaction:Group"] = "test",
				["RecurringTransaction:TriggerName"] = "RecurringTrigger",
			}))
			.ConfigureServices(configureDelegate: (ctx, services) =>
			{
				services.AddPersistence(configuration: ctx.Configuration).AddAuth();
				services.AddApplication();

				services.AddScoped<ITransactionCreationService, TransactionCreationService>();

				services.AddRabbitMqCore();
				services.AddRabbitMqPublisher();

				services.AddScoped<AccountEventApplier>();
				services.AddScoped<AccountProjection>();
				services.AddOptions<ProjectionRetryOptions>()
					 .BindConfiguration(ProjectionRetryOptions.SectionName)
					 .ValidateDataAnnotations();

				services.AddScoped<OutboxPublisherJob>();
				services.AddScoped<BalanceAdjustmentJob>();
				services.AddScoped<TransferCreditLagJob>();
				services.AddScoped<RecurringTransactionHandlingJob>();
				services.AddScoped<RecurringTransactionConsumer>();

				services.AddOptions<OutboxOptions>()
					 .BindConfiguration(OutboxOptions.SectionName)
					 .ValidateDataAnnotations();
				services.AddOptions<BalanceAdjustmentJobOptions>()
					 .BindConfiguration(BalanceAdjustmentJobOptions.SectionName)
					 .ValidateDataAnnotations();
				services.AddOptions<TransferCreditLagOptions>()
					 .BindConfiguration(TransferCreditLagOptions.SectionName)
					 .ValidateDataAnnotations();
				services.AddOptions<RecurringTransactionJobOptions>()
					 .BindConfiguration(RecurringTransactionJobOptions.SectionName)
					 .ValidateDataAnnotations();

				// RabbitMQ listeners start as BackgroundServices with Host
				services.AddRabbitMqListener<AggregateEventsMessage, AccountEventsConsumer>();
				services.AddRabbitMqListener<AggregateEventsMessage, AccountTransferConsumer>();
				services.AddRabbitMqListener<RecurringTransactionTriggeredMessage, RecurringTransactionConsumer>();

				ConfigureAdditionalServices(services: services, configuration: ctx.Configuration);
			})
			.Build();

		await Host.StartAsync();

		Migrator.DatabaseMigrator.Upgrade(connectionString: _connectionString, logToConsole: false);

		Context = Host.Services.GetRequiredService<FinanceTrackerContext>();
		Mediator = Host.Services.GetRequiredService<IMediator>();
	}

	private async Task DeclareRabbitTopologyAsync(Uri rabbitUri, string testRunId)
	{
		const string queueTypeArgument = "x-queue-type";
		const string deliveryLimitArgument = "x-delivery-limit";
		const string delayedRetryTypeArgument = "x-delayed-retry-type";
		const string delayedRetryMinArgument = "x-delayed-retry-min";
		const string delayedRetryMaxArgument = "x-delayed-retry-max";
		const string deadLetterExchangeArgument = "x-dead-letter-exchange";

		const int maxRetries = 3;
		const int delayedRetryMinMs = 1000;
		const int delayedRetryMaxMs = 30000;

		(string Queue, string RoutingKey)[] queues =
		[
			(AccountQueueName(testRunId: testRunId), AggregateTypeNames.Account),
			(TransferQueueName(testRunId: testRunId), AggregateTypeNames.Account),
			(RecurringQueueName(testRunId: testRunId), AggregateTypeNames.RecurringTransaction)
		];

		ConnectionFactory factory = new ConnectionFactory
		{
			HostName = rabbitUri.Host,
			Port = rabbitUri.Port,
			UserName = RabbitData,
			Password = RabbitData
		};

		await using IConnection connection = await factory.CreateConnectionAsync();
		await using IChannel channel = await connection.CreateChannelAsync();

		string exchangeName = ExchangeName(testRunId: testRunId);
		await channel.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic, durable: true);

		foreach ((string queue, string routingKey) in queues)
		{
			string dlxName = $"{queue}.dlx";
			string dlqName = $"{queue}.dlq";

			await channel.ExchangeDeclareAsync(exchange: dlxName, type: ExchangeType.Fanout, durable: true);
			await channel.QueueDeclareAsync(queue: dlqName, durable: true, exclusive: false, autoDelete: false);
			await channel.QueueBindAsync(queue: dlqName, exchange: dlxName, routingKey: String.Empty);

			await channel.QueueDeclareAsync(
				queue: queue,
				durable: true,
				exclusive: false,
				autoDelete: false,
				arguments: new Dictionary<string, object?>
				{
					[queueTypeArgument] = "quorum",
					[deadLetterExchangeArgument] = dlxName,
					[deliveryLimitArgument] = maxRetries,
					[delayedRetryTypeArgument] = "failed",
					[delayedRetryMinArgument] = delayedRetryMinMs,
					[delayedRetryMaxArgument] = delayedRetryMaxMs
				}
			);
			await channel.QueueBindAsync(queue: queue, exchange: exchangeName, routingKey: routingKey);
		}
	}

	protected virtual void ConfigureAdditionalServices(IServiceCollection services, IConfiguration configuration) { }

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await Context.Database.CloseConnectionAsync();
		NpgsqlConnection.ClearAllPools();
		await Context.Database.EnsureDeletedAsync();
		await Context.DisposeAsync();

		try
		{
			IConnectionMultiplexer redis = Host.Services.GetRequiredService<IConnectionMultiplexer>();
			IServer server = redis.GetServer(endpoint: redis.GetEndPoints().First());
			await server.FlushAllDatabasesAsync();
		}
		catch { /* non-critical */ }

		await Host.StopAsync();
		Host.Dispose();

		await DeleteTestQueuesAsync();
	}

	private async Task DeleteTestQueuesAsync()
	{
		try
		{
			ConnectionFactory factory = new ConnectionFactory
			{
				HostName = _rabbitUri.Host,
				Port = _rabbitUri.Port,
				UserName = RabbitData,
				Password = RabbitData
			};

			await using IConnection connection = await factory.CreateConnectionAsync();
			await using IChannel channel = await connection.CreateChannelAsync();

			string[] queues =
			[
				AccountQueueName(testRunId: _testRunId),
				TransferQueueName(testRunId: _testRunId),
				RecurringQueueName(testRunId: _testRunId)
			];

			foreach (string queue in queues)
				await channel.QueueDeleteAsync(queue: queue, ifUnused: false, ifEmpty: false);

			await channel.ExchangeDeleteAsync(exchange: ExchangeName(_testRunId), ifUnused: false);
		}
		catch { /* non-critical */ }
	}

	[After(hookType: Assembly)]
	public static async Task StopContainersAsync()
	{
		await Task.WhenAll(
			_postgres.DisposeAsync().AsTask(),
			_redis.DisposeAsync().AsTask(),
			_rabbitMq.DisposeAsync().AsTask()
		);
	}

	protected FinanceTrackerContext CreateReadContext()
	{
		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>().UseNpgsql(connectionString: _connectionString).Options;
		return new FinanceTrackerContext(options: options);
	}

	private static IJobExecutionContext MockJobContext() =>
		Substitute.For<IJobExecutionContext>();

	protected async Task RunOutboxAsync()
	{
		await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<OutboxPublisherJob>().Execute(context: MockJobContext());
	}

	protected async Task RunBalanceAdjustmentAsync()
	{
		await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<BalanceAdjustmentJob>().Execute(context: MockJobContext());
	}

	protected async Task RunTransferCreditLagAsync()
	{
		await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<TransferCreditLagJob>().Execute(context: MockJobContext());
	}

	protected async Task RunRecurringTransactionJobAsync()
	{
		await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<RecurringTransactionHandlingJob>().Execute(context: MockJobContext());
	}

	protected async Task ProcessRecurringTransactionDirectAsync(RecurringTransactionTriggeredMessage message)
	{
		await using AsyncServiceScope scope = Host.Services.CreateAsyncScope();
		await scope.ServiceProvider.GetRequiredService<RecurringTransactionConsumer>().HandleAsync(message: message);
	}

	protected static async Task WaitForConditionAsync(
		Func<Task<bool>> condition,
		TimeSpan? timeout = null,
		TimeSpan? pollInterval = null)
	{
		TimeSpan deadline = timeout ?? TimeSpan.FromSeconds(seconds: 10);
		TimeSpan poll = pollInterval ?? TimeSpan.FromMilliseconds(milliseconds: 100);
		DateTimeOffset start = DateTimeOffset.UtcNow;

		while (DateTimeOffset.UtcNow - start < deadline)
		{
			if (await condition())
				return;

			await Task.Delay(delay: poll);
		}

		throw new TimeoutException(message: $"Condition not met within {deadline.TotalSeconds}s.");
	}
}
