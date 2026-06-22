using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Tests.Integration._Shared.Fixtures;

public abstract class RabbitMqDatabaseFixture
{
	private const string TemplateDatabaseName = "ft_template";

	private static PostgreSqlContainer _postgresContainer = null!;
	private static RabbitMqContainer _rabbitMqContainer = null!;

	protected FinanceTrackerContext Context { get; private set; } = null!;
	protected EFUnitOfWork UnitOfWork { get; private set; } = null!;
	protected string RabbitMqConnectionString => _rabbitMqContainer.GetConnectionString();
	private string _connectionString = null!;

	[Before(hookType: Assembly)]
	public static async Task StartContainersAsync()
	{
		Task postgresTask = StartPostgresAsync();
		Task rabbitTask = StartRabbitMqAsync();
		await Task.WhenAll(postgresTask, rabbitTask);
	}

	private static async Task StartPostgresAsync()
	{
		_postgresContainer = new PostgreSqlBuilder(image: "postgres:16").WithCommand("-N", "500").Build();
		await _postgresContainer.StartAsync();

		string templateConnectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgresContainer.GetConnectionString())
		{
			Database = TemplateDatabaseName
		}.ConnectionString;

		FinanceTracker.Migrator.DatabaseMigrator.Upgrade(connectionString: templateConnectionString, logToConsole: false);
		NpgsqlConnection.ClearPool(connection: new NpgsqlConnection(connectionString: templateConnectionString));
	}

	private static async Task StartRabbitMqAsync()
	{
		_rabbitMqContainer = new RabbitMqBuilder(image: "rabbitmq:4.3.0")
			.WithUsername(username: "guest")
			.WithPassword(password: "guest")
			.Build();
		await _rabbitMqContainer.StartAsync();
	}

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		string databaseName = $"ft_test_{Guid.CreateVersion7():N}";
		_connectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgresContainer.GetConnectionString())
		{
			Database = databaseName
		}.ConnectionString;

		string adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgresContainer.GetConnectionString())
		{
			Database = "postgres"
		}.ConnectionString;

		await using NpgsqlConnection adminConnection = new NpgsqlConnection(connectionString: adminConnectionString);
		await adminConnection.OpenAsync();
		await using NpgsqlCommand command = new NpgsqlCommand(
			cmdText: $"CREATE DATABASE \"{databaseName}\" TEMPLATE {TemplateDatabaseName}",
			connection: adminConnection
		);
		await command.ExecuteNonQueryAsync();

		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>()
			.UseNpgsql(connectionString: _connectionString)
			.Options;

		Context = new FinanceTrackerContext(options: options);
		UnitOfWork = new EFUnitOfWork(context: Context, logger: NullLogger<EFUnitOfWork>.Instance);
	}

	protected FinanceTrackerContext CreateContext()
	{
		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>()
			.UseNpgsql(connectionString: _connectionString).Options;

		return new FinanceTrackerContext(options: options);
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await UnitOfWork.DisposeAsync();
		await Context.Database.CloseConnectionAsync();
		NpgsqlConnection.ClearAllPools();
		await Context.Database.EnsureDeletedAsync();
		await Context.DisposeAsync();
	}

	[After(hookType: Assembly)]
	public static async Task StopContainersAsync()
	{
		await _postgresContainer.DisposeAsync();
		await _rabbitMqContainer.DisposeAsync();
	}

	protected async Task<(IConnection Connection, IChannel Channel)> CreateChannelAsync(CancellationToken ct = default)
	{
		Uri uri = new Uri(uriString: RabbitMqConnectionString);

		ConnectionFactory factory = new ConnectionFactory
		{
			HostName = uri.Host,
			Port = uri.Port,
			UserName = "guest",
			Password = "guest"
		};

		IConnection connection = await factory.CreateConnectionAsync(cancellationToken: ct);
		IChannel channel = await connection.CreateChannelAsync(cancellationToken: ct);
		return (connection, channel);
	}

	protected static async Task WaitForConditionAsync(
		Func<Task<bool>> condition,
		TimeSpan? timeout = null,
		TimeSpan? pollInterval = null)
	{
		TimeSpan deadline = timeout ?? TimeSpan.FromSeconds(seconds: 15);
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