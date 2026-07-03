using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Benchmarks.Infrastructure;

public sealed class BenchmarkDatabase
{
	private static readonly Lazy<BenchmarkDatabase> _instance = new Lazy<BenchmarkDatabase>(
		valueFactory: () => new BenchmarkDatabase(),
		mode: LazyThreadSafetyMode.ExecutionAndPublication
	);

	public static BenchmarkDatabase Instance => _instance.Value;

	private PostgreSqlContainer _container = null!;
	private string _connectionString = null!;

	public Guid UserId { get; private set; }
	public Guid AccountId { get; private set; }
	public Guid FromAccountId { get; private set; }
	public Guid ToAccountId { get; private set; }
	public Guid CategoryId { get; private set; }
	public Guid ExpenseCategoryId { get; private set; }
	public Guid BudgetId { get; private set; }
	public Guid TransferId { get; private set; }
	public Guid TransactionId { get; private set; }
	public Guid RecurringTransactionId { get; private set; }
	public string RefreshTokenHash { get; private set; } = null!;
	public string MigrationsDirectory { get; private set; } = null!;
	public IReadOnlyList<string> AppliedMigrations { get; private set; } = [];

	private BenchmarkDatabase() { }

	public async Task InitializeAsync()
	{
		_container = new PostgreSqlBuilder(image: "postgres:16")
			.WithLogger(logger: NullLogger<PostgreSqlBuilder>.Instance)
			.Build();

		await _container.StartAsync();

		_connectionString = _container.GetConnectionString();

		await using NpgsqlConnection connection = new NpgsqlConnection(connectionString: _connectionString);
		await connection.OpenAsync();

		await CreateSchemaAsync(connection: connection);
		await SeedDataAsync(connection: connection);
		await VacuumAnalyzeAsync(connection: connection);
	}

	/// <summary>
	/// Without this, the visibility map isn't set right after the bulk seed insert, so Index-Only Scans
	/// (e.g. the covering partial indexes on rm_transactions.is_rate_pending / rm_transfers.status) can't
	/// reliably skip heap fetches yet — whether they do depends on whether autovacuum happened to run by
	/// chance before the benchmark executed, making those specific results inconsistent run to run despite
	/// identical code and indexes. ANALYZE also refreshes planner statistics after the bulk load, which
	/// affects plan choice for every other query in the suite, not just these two.
	/// </summary>
	private static async Task VacuumAnalyzeAsync(NpgsqlConnection connection)
	{
		await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: "VACUUM ANALYZE;", connection: connection);
		cmd.CommandTimeout = 300;
		await cmd.ExecuteNonQueryAsync();
	}

	public FinanceTrackerContext CreateContext()
	{
		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>().UseNpgsql(connectionString: _connectionString).Options;
		return new FinanceTrackerContext(options: options);
	}

	private async Task CreateSchemaAsync(NpgsqlConnection connection)
	{
		string migrationsDir = Path.GetFullPath(path: Path.Combine(
			AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FinanceTracker", "FinanceTracker.Migrator", "Migrations"
		));
		MigrationsDirectory = migrationsDir;

		string[] migrationFiles = Directory
			.GetFiles(path: migrationsDir, searchPattern: "V*.sql")
			.OrderBy(keySelector: f => f)
			.ToArray();

		List<string> applied = new List<string>(capacity: migrationFiles.Length);

		foreach (string file in migrationFiles)
		{
			string sql = await File.ReadAllTextAsync(path: file);
			await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: sql, connection: connection);
			await cmd.ExecuteNonQueryAsync();
			applied.Add(item: Path.GetFileName(path: file));
		}

		AppliedMigrations = applied;
	}

	private async Task SeedDataAsync(NpgsqlConnection connection)
	{
		UserId = Guid.NewGuid();
		AccountId = Guid.NewGuid();
		FromAccountId = Guid.NewGuid();
		ToAccountId = Guid.NewGuid();
		CategoryId = Guid.NewGuid();
		ExpenseCategoryId = Guid.NewGuid();
		BudgetId = Guid.NewGuid();
		TransferId = Guid.NewGuid();
		TransactionId = Guid.NewGuid();
		RecurringTransactionId = Guid.NewGuid();
		RefreshTokenHash = Convert.ToHexString(inArray: Guid.NewGuid().ToByteArray());

		string seedPath = Path.GetFullPath(path: Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Infrastructure", "seed.sql"));

		string template = await File.ReadAllTextAsync(path: seedPath);

		string currentMonthStart = new DateTime(
			year: DateTime.UtcNow.Year,
			month: DateTime.UtcNow.Month,
			day: 1
		).ToString(format: "yyyy-MM-dd");

		string sql = template
			.Replace(oldValue: "{UserId}", newValue: UserId.ToString())
			.Replace(oldValue: "{AccountId}", newValue: AccountId.ToString())
			.Replace(oldValue: "{FromAccountId}", newValue: FromAccountId.ToString())
			.Replace(oldValue: "{ToAccountId}", newValue: ToAccountId.ToString())
			.Replace(oldValue: "{CategoryId}", newValue: CategoryId.ToString())
			.Replace(oldValue: "{ExpenseCategoryId}", newValue: ExpenseCategoryId.ToString())
			.Replace(oldValue: "{BudgetId}", newValue: BudgetId.ToString())
			.Replace(oldValue: "{TransferId}", newValue: TransferId.ToString())
			.Replace(oldValue: "{TransactionId}", newValue: TransactionId.ToString())
			.Replace(oldValue: "{RecurringTransactionId}", newValue: RecurringTransactionId.ToString())
			.Replace(oldValue: "{RefreshTokenHash}", newValue: RefreshTokenHash)
			.Replace(oldValue: "{CurrentMonthStart}", newValue: currentMonthStart);

		await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: sql, connection: connection);
		cmd.CommandTimeout = 600;
		await cmd.ExecuteNonQueryAsync();
	}

	public async Task DisposeAsync()
		=> await _container.DisposeAsync();
}
