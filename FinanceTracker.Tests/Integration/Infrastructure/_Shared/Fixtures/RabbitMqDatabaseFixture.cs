using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;

public abstract class RabbitMqDatabaseFixture
{
	private static PostgreSqlContainer _postgresContainer = null!;
	private static RabbitMqContainer _rabbitMqContainer = null!;

	protected FinanceTrackerContext Context { get; private set; } = null!;
	protected EFUnitOfWork UnitOfWork { get; private set; } = null!;
	protected string RabbitMqConnectionString => _rabbitMqContainer.GetConnectionString();

	[Before(hookType: Assembly)]
	public static async Task StartContainersAsync()
	{
		Task postgresTask = StartPostgresAsync();
		Task rabbitTask = StartRabbitMqAsync();
		await Task.WhenAll(postgresTask, rabbitTask);
	}

	private static async Task StartPostgresAsync()
	{
		_postgresContainer = new PostgreSqlBuilder(image: "postgres:16").WithCommand("-N", "150").Build();
		await _postgresContainer.StartAsync();
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
		string connectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgresContainer.GetConnectionString())
		{
			Database = $"ft_test_{Guid.CreateVersion7():N}"
		}.ConnectionString;

		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>()
			.UseNpgsql(connectionString: connectionString)
			.Options;

		Context = new FinanceTrackerContext(options: options);
		UnitOfWork = new EFUnitOfWork(context: Context, logger: NullLogger<EFUnitOfWork>.Instance);
		await Context.Database.EnsureCreatedAsync();
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
}