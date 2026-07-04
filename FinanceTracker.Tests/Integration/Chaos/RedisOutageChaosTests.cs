using FinanceTracker.Application.Configurations;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace FinanceTracker.Tests.Integration.Chaos;

/// <summary>
/// Verifies that a Redis outage degrades gracefully instead of failing every user command.
/// </summary>
public sealed class RedisOutageChaosTests
{
	private const string TemplateDatabaseName = "ft_chaos_template";

	private PostgreSqlContainer _postgres = null!;
	private RedisContainer _redis = null!;
	private IHost _host = null!;
	private string _databaseName = null!;

	[Before(hookType: Test)]
	public async Task SetupAsync()
	{
		_postgres = new PostgreSqlBuilder(image: "postgres:16").WithCommand("-N", "500").Build();
		_redis = new RedisBuilder(image: "redis:7").Build();
		await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

		string templateConnectionString = new NpgsqlConnectionStringBuilder(connectionString: _postgres.GetConnectionString())
		{
			Database = TemplateDatabaseName
		}.ConnectionString;

		Migrator.DatabaseMigrator.Upgrade(connectionString: templateConnectionString, logToConsole: false);
		NpgsqlConnection.ClearPool(connection: new NpgsqlConnection(connectionString: templateConnectionString));

		_databaseName = $"ft_chaos_{Guid.CreateVersion7():N}";
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
			[$"{RedisOptions.SectionName}:ConnectionString"] = _redis.GetConnectionString() + ",allowAdmin=true,connectTimeout=200,syncTimeout=200,connectRetry=1,abortConnect=false",
			[$"{RedisOptions.SectionName}:InstanceName"] = "ft_chaos:",
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
			services.AddApplication();
		}).Build();

		await _host.StartAsync();
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await _host.StopAsync();
		_host.Dispose();

		await _postgres.DisposeAsync();
		await _redis.DisposeAsync();
	}

	[Test]
	public async Task CreateAccount_WhenRedisIsDown_ShouldStillSucceed()
	{
		IMediator mediator = _host.Services.GetRequiredService<IMediator>();
		Guid userId = await RegisterUserAsync();

		// Sanity check: with Redis up, the command succeeds normally.
		Result<Guid, AppException> beforeOutage = await SendCreateAccountAsync(mediator: mediator, userId: userId);
		await Assert.That(value: beforeOutage.IsSuccess).IsTrue()
			.Because(message: "Baseline command (Redis healthy) should succeed before the outage is introduced.");

		await _redis.StopAsync();

		Result<Guid, AppException> duringOutage = await SendCreateAccountAsync(mediator: mediator, userId: userId);

		await Assert.That(value: duringOutage.IsSuccess).IsTrue()
			.Because(message: "A Redis outage should not fail a user command — RateLimitingBehaviour is expected to fail open.");
	}

	private static Task<Result<Guid, AppException>> SendCreateAccountAsync(IMediator mediator, Guid userId)
	{
		return mediator.Send(request: new CreateAccountCommand(
			UserId: userId,
			Name: Name.Create(value: "Chaos Account").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "USD").Value,
			InitialBalance: 0m
		) { IdempotencyKey = Guid.CreateVersion7() });
	}

	private async Task<Guid> RegisterUserAsync()
	{
		using IServiceScope scope = _host.Services.CreateScope();
		FinanceTrackerContext context = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();

		Guid userId = Guid.CreateVersion7();
		await context.Database.ExecuteSqlInterpolatedAsync($"""
			INSERT INTO users (id, base_currency_code, created_at, email, password_hash)
			VALUES ({userId}, 'USD', now(), {$"chaos-{userId:N}@test.local"}, 'unused')
		""");

		return userId;
	}
}
