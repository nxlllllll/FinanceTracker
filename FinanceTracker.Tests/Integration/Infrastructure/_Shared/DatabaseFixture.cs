using FinanceTracker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared;

[NotInParallel]
public abstract class DatabaseFixture
{
	private static PostgreSqlContainer _container = null!;
	protected FinanceTrackerContext Context { get; private set; } = null!;

	[Before(hookType: Assembly)]
	public static async Task StartContainerAsync()
	{
		_container = new PostgreSqlBuilder(image: "postgres:16").Build();
		await _container.StartAsync();
	}

	[Before(hookType: Test)]
	public async Task SetupDatabaseAsync()
	{
		string connectionString = new NpgsqlConnectionStringBuilder(connectionString: _container.GetConnectionString())
		{
			Database = $"ft_test_{Guid.NewGuid():N}"
		}.ConnectionString;

		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>()
													.UseNpgsql(connectionString: connectionString).Options;

		Context = new FinanceTrackerContext(options: options);
		await Context.Database.EnsureCreatedAsync();
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await Context.Database.CloseConnectionAsync();
		NpgsqlConnection.ClearAllPools();
		await Context.Database.EnsureDeletedAsync();
		await Context.DisposeAsync();
	}

	[After(hookType: Assembly)]
	public static async Task StopContainerAsync()
		=> await _container.DisposeAsync();
}