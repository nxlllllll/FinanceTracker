using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Outbox;
using FinanceTracker.Infrastructure.Database.Repositories;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

		services.AddSingleton<IEventTypeResolver, EventTypeResolver>(implementationFactory: _ =>
			new EventTypeResolver(assembly: typeof(IEvent).Assembly)
		);
		
		services.AddScoped<IEventStore, PostgresEventStore>();
		services.AddScoped<IAccountRepository, AccountRepository>();
		services.AddScoped<IAccountReadRepository, AccountReadRepository>();
		services.AddScoped<IAccountWriteRepository, AccountWriteRepository>();
		services.AddScoped<ICategoryRepository, CategoryRepository>();
		services.AddScoped<ITransactionReadRepository, TransactionReadRepository>();
		services.AddScoped<ITransactionWriteRepository, TransactionWriteRepository>();
		services.AddScoped<ICurrencyRepository, CurrencyRepository>();
		services.AddScoped<IAccountTypeRepository, AccountTypeRepository>();
		services.AddScoped<IUserRepository, UserRepository>();
		services.AddScoped<ICurrencyRateRepository, CurrencyRateRepository>();
		services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();

		services.AddHostedService<OutboxWorker>();
		return services;
	}
}