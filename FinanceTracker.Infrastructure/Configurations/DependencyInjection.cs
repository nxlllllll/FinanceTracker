using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Repositories.Operation;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.Snapshot;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Core.Services.RateLimit;
using FinanceTracker.Core.Services.Rebuild;
using FinanceTracker.Core.Services.Token;
using FinanceTracker.Infrastructure.Cache;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.Idempotency;
using FinanceTracker.Infrastructure.Database.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Repositories.Outbox;
using FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Repositories.Snapshot;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;
using FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Infrastructure.Services.Auth;
using FinanceTracker.Infrastructure.Services.Correlation;
using FinanceTracker.Infrastructure.Services.Currency;
using FinanceTracker.Infrastructure.Services.Date;
using FinanceTracker.Infrastructure.Services.Password;
using FinanceTracker.Infrastructure.Services.RateLimit;
using FinanceTracker.Infrastructure.Services.Rebuild.Account;
using FinanceTracker.Infrastructure.Services.Token;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FinanceTracker.Infrastructure.Configurations;

public static class DependencyInjection
{
	/// <summary>
	/// Registers persistence, caching, and event-sourcing infrastructure shared by
	/// every host:DbContext, event store, all repositories, Redis-backed
	/// cache/rate-limiting, currency conversion, and the unit of work.
	/// </summary>
	public static IServiceCollection AddPersistence(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddOptions<EventStoreOptions>()
			.BindConfiguration(configSectionPath: EventStoreOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<RedisOptions>()
			.BindConfiguration(configSectionPath: RedisOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddDbContext<FinanceTrackerContext>(optionsAction: options =>
			options.UseNpgsql(connectionString: configuration.GetConnectionString(name: nameof(FinanceTrackerContext)))
		);

		RedisOptions redisOptions = configuration.GetSection(key: RedisOptions.SectionName).Get<RedisOptions>()
			?? throw new ConfigurationException(message: "Redis configuration is missing.");

		services.AddStackExchangeRedisCache(setupAction: options =>
		{
			options.Configuration = redisOptions.ConnectionString;
			options.InstanceName = redisOptions.InstanceName;
		});

		services.AddSingleton<IConnectionMultiplexer>(implementationFactory: _ => ConnectionMultiplexer.Connect(configuration: redisOptions.ConnectionString));

		services.AddOptions<RateLimiterFallbackOptions>()
			.BindConfiguration(configSectionPath: RateLimiterFallbackOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<InMemoryRateLimiterOptions>()
			.BindConfiguration(configSectionPath: InMemoryRateLimiterOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton<IRateLimiter, RedisRateLimiter>();
		services.AddSingleton<InMemoryRateLimiter>();
		services.Decorate<IRateLimiter, FallbackRateLimiter>();

		services.AddSingleton<IEventTypeResolver, EventTypeResolver>(implementationFactory: s => new EventTypeResolver(
			assembly: typeof(IEvent).Assembly,
			logger: s.GetService<ILogger<EventTypeResolver>>()!
		));

		services.AddSingleton<IIntegrationEventMapper, AccountIntegrationEventMapper>();

		services.AddSingleton<IIntegrationEventTypeResolver, IntegrationEventTypeResolver>(implementationFactory: s => new IntegrationEventTypeResolver(
			contractsAssembly: typeof(IIntegrationEvent).Assembly,
			logger: s.GetRequiredService<ILogger<IntegrationEventTypeResolver>>()
		));

		services.Scan(scan => scan
			.FromAssemblyOf<EventUpcasterRegistry>()
			.AddClasses(classes => classes.AssignableTo(typeof(EventUpcaster<,>)))
			.AsSelf()
			.WithSingletonLifetime()
		);

		services.Scan(scan => scan
			.FromAssemblyOf<EventUpcasterRegistry>()
			.AddClasses(classes => classes.AssignableTo<IEventUpcaster>())
			.AsImplementedInterfaces()
			.WithSingletonLifetime()
		);
		services.AddSingleton<IEventUpcasterRegistry, EventUpcasterRegistry>();

		services.AddScoped<IEventStore, PostgresEventStore>();

		// Account
		services.AddScoped<IAccountRepository, AccountRepository>();
		services.AddScoped<IAccountReadRepository, AccountReadRepository>();
		services.AddScoped<IAccountWriteRepository, AccountWriteRepository>();

		// Budget
		services.AddScoped<IBudgetRepository, BudgetRepository>();
		services.AddScoped<IBudgetReadRepository, BudgetReadRepository>();
		services.AddScoped<IBudgetWriteRepository, BudgetWriteRepository>();
		services.AddScoped<IBudgetProgressReadRepository, BudgetProgressReadRepository>();
		services.AddScoped<IBudgetProgressWriteRepository, BudgetProgressWriteRepository>();

		// Category
		services.AddScoped<ICategoryRepository, CategoryRepository>();
		services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
		services.AddScoped<ICategoryWriteRepository, CategoryWriteRepository>();
		services.AddScoped<ICategoryTotalWriteRepository, CategoryTotalWriteRepository>();
		services.AddScoped<ICategoryTotalReadRepository, CategoryTotalReadRepository>();

		// Currency
		services.AddScoped<ICurrencyReadRepository, CurrencyReadRepository>();
		services.Decorate<ICurrencyReadRepository, CachedCurrencyReadRepository>();
		services.AddScoped<ICurrencyRateReadRepository, CurrencyRateReadRepository>();
		services.Decorate<ICurrencyRateReadRepository, CachedCurrencyRateReadRepository>();
		services.AddScoped<ICurrencyRateWriteRepository, CurrencyRateWriteRepository>();
		services.Decorate<ICurrencyRateWriteRepository, CachedCurrencyRateWriteRepository>();

		// RecurringTransaction
		services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
		services.AddScoped<IRecurringTransactionReadRepository, RecurringTransactionReadRepository>();
		services.AddScoped<IRecurringTransactionWriteRepository, RecurringTransactionWriteRepository>();

		// Transaction
		services.AddScoped<ITransactionRepository, TransactionRepository>();
		services.AddScoped<ITransactionReadRepository, TransactionReadRepository>();
		services.AddScoped<ITransactionWriteRepository, TransactionWriteRepository>();

		// Transfer
		services.AddScoped<ITransferRepository, TransferRepository>();
		services.AddScoped<ITransferWriteRepository, TransferWriteRepository>();
		services.AddScoped<ITransferReadRepository, TransferReadRepository>();

		// User
		services.AddScoped<IUserAuthRepository, UserReadRepository>();
		services.AddScoped<IUserQueryRepository, UserReadRepository>();
		services.AddScoped<IUserWriteRepository, UserWriteRepository>();
		services.AddScoped<IUserSessionReadRepository, UserSessionReadRepository>();
		services.AddScoped<IUserSessionWriteRepository, UserSessionWriteRepository>();

		// UnresolvableEvent
		services.AddScoped<IUnresolvableEventReadRepository, UnresolvableEventReadRepository>();
		services.AddScoped<IUnresolvableEventWriteRepository, UnresolvableEventWriteRepository>();

		// Idempotency
		services.AddScoped<IIdempotencyReadRepository, IdempotencyReadRepository>();
		services.AddScoped<IIdempotencyWriteRepository, IdempotencyWriteRepository>();

		// ProcessedMessage
		services.AddScoped<IProcessedMessageReadRepository, ProcessedMessageReadRepository>();
		services.AddScoped<IProcessedMessageWriteRepository, ProcessedMessageWriteRepository>();

		// Outbox
		services.AddScoped<IOutboxReadRepository, OutboxReadRepository>();
		services.AddScoped<IOutboxWriteRepository, OutboxWriteRepository>();

		// Snapshot
		services.AddScoped<ISnapshotWriteRepository, SnapshotWriteRepository>();

		// Operation
		services.AddScoped<IOperationWriteRepository, OperationWriteRepository>();

		services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
		services.AddScoped<ICorrelationContext, CorrelationContext>();
		services.AddSingleton<IDateProvider, DateProvider>();
		services.AddSingleton<ISnapshotSerializer<Account>, AccountSnapshotSerializer>();

		services.AddScoped<AccountDomainEventApplier>();
		services.AddScoped<IAccountProjectionRebuilder, AccountProjectionRebuilder>();

		services.AddScoped<IUnitOfWork, EFUnitOfWork>();

		services.AddSingleton<RedisCache>();

		return services;
	}

	/// <summary>Registers JWT issuance and password hashing</summary>
	public static IServiceCollection AddAuth(this IServiceCollection services)
	{
		services.AddOptions<Argon2Options>()
			.BindConfiguration(configSectionPath: Argon2Options.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<JwtOptions>()
			.BindConfiguration(configSectionPath: JwtOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddScoped<ITokenService, JwtTokenService>();
		services.AddScoped<ISessionIssuer, SessionIssuer>();
		services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();

		return services;
	}
}
