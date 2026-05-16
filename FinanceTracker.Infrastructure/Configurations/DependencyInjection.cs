using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Abstractions.ES;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Core.Domains.Abstractions.ES.Upcast;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.AccountType;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Repositories.Operations;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.EventMapper;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.AccountType;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Infrastructure.Database.Repositories.BudgetProgress;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Repositories.CategoryTotal;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.CurrencyRate;
using FinanceTracker.Infrastructure.Database.Repositories.Idempotency;
using FinanceTracker.Infrastructure.Database.Repositories.Operations;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Repositories.Transfers;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Infrastructure.Services.Correlation;
using FinanceTracker.Infrastructure.Services.Currency;
using FinanceTracker.Infrastructure.Services.Date;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Infrastructure.Configurations;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddDbContext<FinanceTrackerContext>(optionsAction: options =>
			options.UseNpgsql(connectionString: configuration.GetConnectionString(name: nameof(FinanceTrackerContext)))
		);

		services.AddSingleton<IEventTypeResolver, EventTypeResolver>(implementationFactory: s => new EventTypeResolver(
			assembly: typeof(IEvent).Assembly,
			logger: s.GetService<ILogger<EventTypeResolver>>()!
		));
		
		services.AddSingleton<IIntegrationEventMapper, AccountIntegrationEventMapper>();

		services.AddSingleton<IIntegrationEventTypeResolver, IntegrationEventTypeResolver>(implementationFactory: s => new IntegrationEventTypeResolver(
			contractsAssembly: typeof(IAccountIntegrationEvent).Assembly,
			logger: s.GetRequiredService<ILogger<IntegrationEventTypeResolver>>()
		));
		
		services.Scan(scan => scan
		    .FromAssemblyOf<EventUpcasterRegistry>()
		    .AddClasses(classes => classes.AssignableTo<IEventUpcaster>())
		    .AsImplementedInterfaces()
		    .WithSingletonLifetime()
		);
		services.AddSingleton<IEventUpcasterRegistry, EventUpcasterRegistry>();
		
		services.AddScoped<IEventStore, PostgresEventStore>();
		
		services.AddScoped<IAccountRepository, AccountRepository>();
		services.AddScoped<IAccountReadRepository, AccountReadRepository>();
		services.AddScoped<IAccountWriteRepository, AccountWriteRepository>();

		services.AddScoped<IAccountTypeReadRepository, AccountTypeReadRepository>();
		
		services.AddScoped<IBudgetReadRepository, BudgetReadRepository>();
		services.AddScoped<IBudgetWriteRepository, BudgetWriteRepository>();

		services.AddScoped<IBudgetProgressReadRepository, BudgetProgressReadRepository>();
		services.AddScoped<IBudgetProgressWriteRepository, BudgetProgressWriteRepository>();

		services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
		services.AddScoped<ICategoryWriteRepository, CategoryWriteRepository>();
		
		services.AddScoped<ICategoryTotalWriteRepository, CategoryTotalWriteRepository>();
		services.AddScoped<ICategoryTotalReadRepository, CategoryTotalReadRepository>();
		
		services.AddScoped<ICurrencyReadRepository, CurrencyReadRepository>();
		
		services.AddScoped<ICurrencyRateReadRepository, CurrencyRateReadRepository>();
		
		services.AddScoped<IRecurringTransactionReadRepository, RecurringTransactionReadRepository>();
		services.AddScoped<IRecurringTransactionWriteRepository, RecurringTransactionWriteRepository>();
		
		services.AddScoped<ITransactionReadRepository, TransactionReadRepository>();
		services.AddScoped<ITransactionWriteRepository, TransactionWriteRepository>();

		services.AddScoped<ITransferWriteRepository, TransferWriteRepository>();
		services.AddScoped<ITransferReadRepository, TransferReadRepository>();
		
		services.AddScoped<IUserReadRepository, UserReadRepository>();
		services.AddScoped<IUserWriteRepository, UserWriteRepository>();

		services.AddScoped<IOperationsWriteRepository, OperationsWriteRepository>();
		
		services.AddScoped<IIdempotencyReadRepository, IdempotencyReadRepository>();
		services.AddScoped<IIdempotencyWriteRepository, IdempotencyWriteRepository>();
		
		services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
		services.AddScoped<IDateProvider, DateProvider>();
		services.AddScoped<ICorrelationContext, CorrelationContext>();
		
		services.AddScoped<IUnitOfWork, EFUnitOfWork>();
		
		services.AddOptions<EventStoreOptions>()
			.BindConfiguration(configSectionPath: EventStoreOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		
		return services;
	}
}