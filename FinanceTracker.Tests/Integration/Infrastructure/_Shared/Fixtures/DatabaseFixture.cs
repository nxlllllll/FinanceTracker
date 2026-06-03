using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;

public abstract class DatabaseFixture
{
	private static PostgreSqlContainer _container = null!;
	protected FinanceTrackerContext Context { get; private set; } = null!;
	protected EFUnitOfWork UnitOfWork { get; private set; } = null!;

	[Before(hookType: Assembly)]
	public static async Task StartContainerAsync()
	{
		_container = new PostgreSqlBuilder(image: "postgres:16").WithCommand("-N", "150").Build();
		await _container.StartAsync();
	}

	[Before(hookType: Test)]
	public async Task SetupDatabaseAsync()
	{
		string connectionString = new NpgsqlConnectionStringBuilder(connectionString: _container.GetConnectionString())
		{
			Database = $"ft_test_{Guid.CreateVersion7():N}"
		}.ConnectionString;

		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>()
													.UseNpgsql(connectionString: connectionString).Options;

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
	public static async Task StopContainerAsync()
		=> await _container.DisposeAsync();
}