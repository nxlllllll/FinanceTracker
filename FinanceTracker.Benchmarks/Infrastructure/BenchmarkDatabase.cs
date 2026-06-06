using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
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
	public Guid BudgetId { get; private set; }
	public string RefreshTokenHash { get; private set; } = null!;

	private BenchmarkDatabase() { }

	public async Task InitializeAsync()
	{
		_container = new PostgreSqlBuilder(image: "postgres:16")
			.WithLogger(logger: NullLogger<PostgreSqlBuilder>.Instance)
			.WithCommand("-N", "200", "-c", "shared_preload_libraries=pg_stat_statements")
			.Build();

		await _container.StartAsync();

		_connectionString = _container.GetConnectionString();

		await using NpgsqlConnection connection = new NpgsqlConnection(connectionString: _connectionString);
		await connection.OpenAsync();

		await CreateSchemaAsync(connection: connection);
		await SeedDataAsync(connection: connection);
	}

	public FinanceTrackerContext CreateContext()
	{
		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>().UseNpgsql(connectionString: _connectionString).Options;
		return new FinanceTrackerContext(options: options);
	}
	
	private static async Task CreateSchemaAsync(NpgsqlConnection connection)
	{
		string migrationsDir = Path.GetFullPath(path: Path.Combine(
			AppContext.BaseDirectory, "..", "..", "..", "..", "..", "FinanceTracker", "FinanceTracker.Migrator", "Migrations"
		));

		IEnumerable<string> migrationFiles = Directory.GetFiles(path: migrationsDir, searchPattern: "V*.sql").OrderBy(keySelector: f => f);

		foreach (string file in migrationFiles)
		{
			string sql = await File.ReadAllTextAsync(path: file);
			await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: sql, connection: connection);
			await cmd.ExecuteNonQueryAsync();
		}
	}

	private async Task SeedDataAsync(NpgsqlConnection connection)
	{
		UserId = Guid.NewGuid();
		AccountId = Guid.NewGuid();
		FromAccountId = Guid.NewGuid();
		ToAccountId = Guid.NewGuid();
		CategoryId = Guid.NewGuid();
		BudgetId = Guid.NewGuid();
		RefreshTokenHash = Convert.ToHexString(inArray: Guid.NewGuid().ToByteArray());

		string sql = $"""
			INSERT INTO users (id, email, password_hash, base_currency_code)
			SELECT
				CASE WHEN i = 1 THEN '{UserId}'::uuid ELSE gen_random_uuid() END,
				'user' || i || '@bench.com',
				'hash',
				'RUB'
			FROM generate_series(1, 10) i
			ON CONFLICT DO NOTHING;

			INSERT INTO accounts (id, user_id, name, account_type_code, currency_code)
			SELECT
				CASE
					WHEN u.rn = 1 AND a.i = 1 THEN '{AccountId}'::uuid
					WHEN u.rn = 1 AND a.i = 2 THEN '{FromAccountId}'::uuid
					WHEN u.rn = 1 AND a.i = 3 THEN '{ToAccountId}'::uuid
					ELSE gen_random_uuid()
				END,
				u.id,
				'Account ' || a.i,
				'checking',
				'RUB'
			FROM (SELECT id, ROW_NUMBER() OVER () AS rn FROM users) u
			CROSS JOIN generate_series(1, 50) a(i);

			INSERT INTO rm_account_balances (account_id, balance, last_version, updated_at)
			SELECT id, 100000.00, 1, now()
			FROM accounts;

			INSERT INTO categories (id, user_id, name, type_code)
			SELECT
				CASE WHEN u.rn = 1 AND c.i = 1 THEN '{CategoryId}'::uuid ELSE gen_random_uuid() END,
				u.id,
				'Category ' || c.i,
				CASE WHEN c.i % 2 = 0 THEN 'expense' ELSE 'income' END
			FROM (SELECT id, ROW_NUMBER() OVER () AS rn FROM users) u
			CROSS JOIN generate_series(1, 100) c(i);

			INSERT INTO budgets (id, user_id, category_id, amount, currency_code, date_from, date_to)
			SELECT
				CASE WHEN ROW_NUMBER() OVER () = 1 THEN '{BudgetId}'::uuid ELSE gen_random_uuid() END,
				'{UserId}'::uuid,
				id,
				10000.00,
				'RUB',
				'2025-01-01',
				'2025-12-31'
			FROM categories
			WHERE user_id = '{UserId}'::uuid
			LIMIT 50;

			INSERT INTO rm_budget_progress (budget_id, spent, updated_at)
			SELECT id, 0, now() FROM budgets;

			INSERT INTO rm_transactions (id, account_id, user_id, category_id, amount, currency_code, direction_type, exchange_rate, is_excluded, description, is_rate_pending, occurred_at)
			SELECT
				gen_random_uuid(),
				'{AccountId}'::uuid,
				'{UserId}'::uuid,
				'{CategoryId}'::uuid,
				(random() * 9900 + 100)::numeric(18,2),
				'RUB',
				CASE WHEN i % 2 = 0 THEN 'debit' ELSE 'credit' END,
				1.0,
				false,
				'Bench tx ' || i,
				false,
				now() - (random() * interval '365 days')
			FROM generate_series(1, 1000000) i;

			INSERT INTO rm_transfers (id, user_id, from_account_id, to_account_id, amount_from, currency_from, amount_to, currency_to, exchange_rate, description, is_rate_pending, occurred_at)
			SELECT
				gen_random_uuid(),
				'{UserId}'::uuid,
				'{FromAccountId}'::uuid,
				'{ToAccountId}'::uuid,
				(random() * 9900 + 100)::numeric(18,2),
				'RUB',
				(random() * 9900 + 100)::numeric(18,2),
				'RUB',
				1.0,
				'Bench transfer ' || i,
				false,
				now() - (random() * interval '365 days')
			FROM generate_series(1, 500000) i;

			INSERT INTO rm_category_totals (id, user_id, category_id, period, total, transaction_count, updated_at)
			SELECT
				gen_random_uuid(),
				'{UserId}'::uuid,
				'{CategoryId}'::uuid,
				date_trunc('month', now() - (i || ' months')::interval)::date,
				(random() * 100000)::numeric(18,2),
				(random() * 100)::int,
				now()
			FROM generate_series(0, 23) i;

			INSERT INTO currency_rates (base_code, target_code, rate, actual_at)
			SELECT 'USD', 'RUB', (85 + random() * 10)::numeric(18,6), (now() - (i || ' days')::interval)::date
			FROM generate_series(0, 730) i
			ON CONFLICT DO NOTHING;

			INSERT INTO currency_rates (base_code, target_code, rate, actual_at)
			SELECT 'EUR', 'RUB', (90 + random() * 10)::numeric(18,6), (now() - (i || ' days')::interval)::date
			FROM generate_series(0, 730) i
			ON CONFLICT DO NOTHING;

			INSERT INTO recurring_transactions (id, user_id, account_id, category_id, amount, direction_type, day_of_month, description, currency_code)
			SELECT
				gen_random_uuid(),
				'{UserId}'::uuid,
				'{AccountId}'::uuid,
				'{CategoryId}'::uuid,
				(random() * 9900 + 100)::numeric(18,2),
				CASE WHEN i % 2 = 0 THEN 'debit' ELSE 'credit' END,
				(i % 28 + 1)::smallint,
				'Recurring ' || i,
				'RUB'
			FROM generate_series(1, 100000) i;

			INSERT INTO user_sessions (id, user_id, refresh_token_hash, expires_at, created_at)
			VALUES (gen_random_uuid(), '{UserId}'::uuid, '{RefreshTokenHash}', now() + interval '7 days', now())
			ON CONFLICT DO NOTHING;
			""";

		await using NpgsqlCommand cmd = new NpgsqlCommand(cmdText: sql, connection: connection);
		cmd.CommandTimeout = 300;
		await cmd.ExecuteNonQueryAsync();
	}

	public async Task DisposeAsync()
		=> await _container.DisposeAsync();
}