using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FinanceTracker.Tests.Integration.Infrastructure;

public abstract class DatabaseFixture
{
	private static PostgreSqlContainer _container = null!;
	protected FinanceTrackerContext Context { get; private set; } = null!;

	protected PostgresEventStore CreateEventStore()
	{
		return new PostgresEventStore(context: new FinanceTrackerContext(
			new DbContextOptionsBuilder<FinanceTrackerContext>()
			.UseNpgsql(connectionString: Context.Database.GetConnectionString()!).Options
		), eventTypeResolver: new EventTypeResolver(assembly: typeof(IEvent).Assembly));
	}
	
	protected async Task<string> CreateCurrencyAsync(string code = "RUB")
	{
		bool exists = await Context.Currencies.AnyAsync(c => c.Code == code);
		if (exists) 
			return code;
		
		await Context.Currencies.AddAsync(new CurrencyEntity()
		{
			Code = code,
			Name = code switch
			{
				"RUB" => "Российский рубль",
				"USD" => "Доллар США",
				"EUR" => "Евро",
				_ => code
			},
			Symbol = code switch
			{
				"RUB" => "₽",
				"USD" => "$",
				"EUR" => "€",
				_ => code
			},
			IsActive = true
		});
		await Context.SaveChangesAsync();
		return code;
	}

	protected async Task<string> CreateAccountTypeAsync(string type = "checking")
	{
		bool exists = await Context.AccountTypes.AnyAsync(a => a.Type == type);
		if (exists) 
			return type;
		
		await Context.AccountTypes.AddAsync(new AccountTypeEntity()
		{
			Type = type,
			Name = type switch
			{
				"checking" => "Текущий счёт",
				"savings" => "Сберегательный счёт",
				_ => type
			},
			Description = null
		});
		await Context.SaveChangesAsync();
		return type;
	}

	protected async Task<Guid> CreateUserAsync(string currencyCode = "RUB")
	{
		Guid userId = Guid.NewGuid();
		await Context.Users.AddAsync(new UserEntity()
		{
			Id = userId,
			Email = $"{userId}@test.com",
			PasswordHash = "hash",
			BaseCurrencyCode = currencyCode,
			CreatedAt = DateTime.UtcNow
		});
		await Context.SaveChangesAsync();
		return userId;
	}

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