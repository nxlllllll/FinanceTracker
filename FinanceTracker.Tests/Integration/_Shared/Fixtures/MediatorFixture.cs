using FinanceTracker.Application.Configurations;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Worker.AccountProjection.Consumer;
using FinanceTracker.Worker.AccountProjection.Projection;
using FinanceTracker.Worker.Shared.Projection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FinanceTracker.Tests.Integration._Shared.Fixtures;

/// <summary>
/// Raises a full DI container via <see cref="IHost"/> with a real MediatR pipeline
/// (all behaviours), real Postgres and real Redis.
/// It is used for flow tests that check the complete chain from the command to the read model.
/// </summary>
public abstract class MediatorFixture
{
	private const string TemplateDatabaseName = "ft_template";

	private static PostgreSqlContainer _postgres = null!;
	private static RedisContainer _redis = null!;

	protected IHost Host = null!;
	private string _connectionString = null!;

	protected IMediator Mediator { get; private set; } = null!;
	protected FinanceTrackerContext Context { get; private set; } = null!;

	[Before(hookType: Assembly)]
	public static async Task StartContainersAsync()
	{
		Task postgres = Task.Run(async () =>
		{
			_postgres = new PostgreSqlBuilder(image: "postgres:16").WithCommand("-N", "500").Build();
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

		await Task.WhenAll(postgres, redis);
	}

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		string databaseName = $"ft_flow_{Guid.CreateVersion7():N}";
		_connectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
		{
			Database = databaseName
		}.ConnectionString;

		string redisConnectionString = _redis.GetConnectionString() + ",allowAdmin=true";

		Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().ConfigureAppConfiguration(configureDelegate: (_, builder) => builder.AddInMemoryCollection(initialData: new Dictionary<string, string?>
		{
			[$"ConnectionStrings:{nameof(FinanceTrackerContext)}"] = _connectionString,
			[$"{RedisOptions.SectionName}:ConnectionString"] = redisConnectionString,
			[$"{RedisOptions.SectionName}:InstanceName"] = "ft_test:",
			[$"{EventStoreOptions.SectionName}:SnapshotThreshold"] = "25",
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
			["ProjectionRetry:MaxRetries"] = "3",
			["ProjectionRetry:BaseDelayMs"] = "5",
			["ProjectionRetry:UseJitter"] = "false",
		})).ConfigureServices(configureDelegate: (ctx, services) =>
		{
			services.AddPersistence(configuration: ctx.Configuration).AddAuth();
			services.AddApplication();

			services.AddScoped<AccountEventApplier>();
			services.AddScoped<AccountProjection>();
			services.AddScoped<AccountEventsConsumer>();

			services.AddOptions<ProjectionRetryOptions>()
				.BindConfiguration(configSectionPath: ProjectionRetryOptions.SectionName)
				.ValidateDataAnnotations();
		}).Build();

		string adminConnectionString = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
		{
			Database = "postgres"
		}.ConnectionString;

		await using (NpgsqlConnection adminConnection = new NpgsqlConnection(connectionString: adminConnectionString))
		{
			await adminConnection.OpenAsync();
			await using NpgsqlCommand command = new NpgsqlCommand(
				cmdText: $"CREATE DATABASE \"{databaseName}\" TEMPLATE {TemplateDatabaseName}",
				connection: adminConnection
			);
			await command.ExecuteNonQueryAsync();
		}

		await Host.StartAsync();

		Context = Host.Services.GetRequiredService<FinanceTrackerContext>();
		Mediator = Host.Services.GetRequiredService<IMediator>();
	}

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
	}

	[After(hookType: Assembly)]
	public static async Task StopContainersAsync()
	{
		await _postgres.DisposeAsync();
		await _redis.DisposeAsync();
	}

	/// <summary>Creates an independent DbContext on the same database to test side effects.</summary>
	protected FinanceTrackerContext CreateReadContext()
	{
		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>().UseNpgsql(connectionString: _connectionString).Options;
		return new FinanceTrackerContext(options: options);
	}

	/// <summary>
	/// Applies all domain events of the account to the read model via AccountProjection.
	/// The equivalent of a worker's job, but without RabbitMQ and outbox, is for flow tests.
	/// </summary>
	protected async Task ProjectAccountEventsAsync(Guid accountId)
	{
		using IServiceScope scope = Host.Services.CreateScope();

		IEventStore eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();
		IIntegrationEventMapper mapper = scope.ServiceProvider.GetRequiredService<IIntegrationEventMapper>();
		AccountEventApplier applier = scope.ServiceProvider.GetRequiredService<AccountEventApplier>();

		EventStoreResult result = await eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account
		);

		foreach (IEvent domainEvent in result.Events)
		{
			IIntegrationEvent? integrationEvent = mapper.Map(@event: domainEvent);
			if (integrationEvent is not null)
				await applier.ApplyAsync(@event: integrationEvent);
		}
	}
}
