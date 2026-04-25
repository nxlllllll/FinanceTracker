using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Outbox;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.AccountType;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Repositories.CategoryTotal;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.CurrencyRate;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Repositories.Transfers;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Database.UOW;
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
		services.AddScoped<ITransferWriteRepository, TransferWriteRepository>();
		services.AddScoped<ICategoryTotalWriteRepository, CategoryTotalWriteRepository>();
		services.AddScoped<ICategoryTotalReadRepository, CategoryTotalReadRepository>();
		
		services.AddScoped<IUnitOfWork, EFUnitOfWork>();

		services.AddHostedService<OutboxWorker>();
		return services;
	}
}