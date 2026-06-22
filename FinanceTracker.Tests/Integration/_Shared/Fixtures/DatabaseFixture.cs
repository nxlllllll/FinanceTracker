using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Tests.Integration._Shared.Fixtures;

public abstract class DatabaseFixture
{
	private const string TemplateDatabaseName = "ft_template";

	private static PostgreSqlContainer _container = null!;
	protected FinanceTrackerContext Context { get; private set; } = null!;
	protected EFUnitOfWork UnitOfWork { get; private set; } = null!;
	private string _connectionString = null!;

	[Before(hookType: Assembly)]
	public static async Task StartContainerAsync()
	{
		_container = new PostgreSqlBuilder(image: "postgres:16").WithCommand("-N", "500").Build();
		await _container.StartAsync();

		string templateConnectionString = new NpgsqlConnectionStringBuilder(connectionString: _container.GetConnectionString())
		{
			Database = TemplateDatabaseName
		}.ConnectionString;

		Migrator.DatabaseMigrator.Upgrade(connectionString: templateConnectionString, logToConsole: false);
		NpgsqlConnection.ClearPool(connection: new NpgsqlConnection(connectionString: templateConnectionString));
	}

	[Before(hookType: Test)]
	public async Task SetupDatabaseAsync()
	{
		string databaseName = $"ft_test_{Guid.CreateVersion7():N}";
		_connectionString = new NpgsqlConnectionStringBuilder(connectionString: _container.GetConnectionString())
		{
			Database = databaseName
		}.ConnectionString;

		string adminConnectionString = new NpgsqlConnectionStringBuilder(connectionString: _container.GetConnectionString())
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
			.UseNpgsql(connectionString: _connectionString).Options;

		Context = new FinanceTrackerContext(options: options);
		UnitOfWork = new EFUnitOfWork(context: Context, logger: NullLogger<EFUnitOfWork>.Instance);
	}

	protected FinanceTrackerContext CreateAdditionalContext()
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
	public static async Task StopContainerAsync()
		=> await _container.DisposeAsync();
}