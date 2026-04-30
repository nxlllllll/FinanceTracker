using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.AccountType;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Jobs.Outbox;
using FinanceTracker.Infrastructure.Database.Jobs.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.AccountType;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Infrastructure.Database.Repositories.BudgetProgress;
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
using Quartz;

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
		services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
		services.AddScoped<ICategoryWriteRepository, CategoryWriteRepository>();
		services.AddScoped<ITransactionReadRepository, TransactionReadRepository>();
		services.AddScoped<ITransactionWriteRepository, TransactionWriteRepository>();
		services.AddScoped<ICurrencyReadRepository, CurrencyReadRepository>();
		services.AddScoped<IAccountTypeReadRepository, AccountTypeReadRepository>();
		services.AddScoped<IUserReadRepository, UserReadRepository>();
		services.AddScoped<IUserWriteRepository, UserWriteRepository>();
		services.AddScoped<ICurrencyRateReadRepository, CurrencyRateReadRepository>();
		services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
		services.AddScoped<ITransferWriteRepository, TransferWriteRepository>();
		services.AddScoped<ICategoryTotalWriteRepository, CategoryTotalWriteRepository>();
		services.AddScoped<ICategoryTotalReadRepository, CategoryTotalReadRepository>();
		services.AddScoped<IBudgetReadRepository, BudgetReadRepository>();
		services.AddScoped<IBudgetWriteRepository, BudgetWriteRepository>();
		services.AddScoped<IBudgetProgressReadRepository, BudgetProgressReadRepository>();
		services.AddScoped<IBudgetProgressWriteRepository, BudgetProgressWriteRepository>();
		
		services.AddScoped<IUnitOfWork, EFUnitOfWork>();
		
		services.AddQuartz(configure: configurator =>
		{
		    configurator.AddJob<RecurringTransactionHandlingJob>(
		        configure: configure => configure.WithIdentity(name: nameof(RecurringTransactionHandlingJob), group: "default")
		    );

		    configurator.AddTrigger(configure => configure
		        .ForJob(jobName: nameof(RecurringTransactionHandlingJob), jobGroup: "default")
		        .WithIdentity(name: "RecurringTransactionTrigger", group: "default")
		        .WithCronSchedule(
		            cronExpression: "0 0 3 * * ?",
		            schedule => schedule.InTimeZone(tz: TimeZoneInfo.Utc).WithMisfireHandlingInstructionFireAndProceed()
		        )
		    );

		    configurator.AddJob<OutboxMessagesHandlingJob>(
		        configure: configure => configure.WithIdentity(name: nameof(OutboxMessagesHandlingJob), group: "default")
		    );

		    configurator.AddTrigger(configure: configure => configure
		        .ForJob(jobName: nameof(OutboxMessagesHandlingJob), jobGroup: "default")
		        .WithIdentity(name: "OutboxWorkerTrigger", group: "default")
		        .WithSimpleSchedule(action: schedule => schedule
		            .WithIntervalInSeconds(seconds: 3)
		            .RepeatForever()
		        )
		    );
		});

		services.AddQuartzHostedService(configure: options => options.WaitForJobsToComplete = true);
		
		return services;
	}
}