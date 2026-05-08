using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.AccountType;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Jobs.DeadLetterMonitoring;
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
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Repositories.Transfers;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

		services.AddSingleton<IEventTypeResolver, EventTypeResolver>(implementationFactory: s => new EventTypeResolver(
			assembly: typeof(IEvent).Assembly,
			logger: s.GetService<ILogger<EventTypeResolver>>()!
		));
		
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
		
		services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
		services.AddScoped<IDateProvider, DateProvider>();
		
		services.AddScoped<IUnitOfWork, EFUnitOfWork>();
		
		services.AddOptions<OutboxOptions>()
			.BindConfiguration(configSectionPath: OutboxOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<DeadLetterMonitoringOptions>()
			.BindConfiguration(configSectionPath: DeadLetterMonitoringOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<RecurringTransactionJobOptions>()
			.BindConfiguration(configSectionPath: RecurringTransactionJobOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<EventStoreOptions>()
			.BindConfiguration(configSectionPath: EventStoreOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		OutboxOptions outboxOptions = configuration.GetSection(key: OutboxOptions.SectionName).Get<OutboxOptions>() ?? new OutboxOptions();
		DeadLetterMonitoringOptions deadLetterOptions = configuration.GetSection(key: DeadLetterMonitoringOptions.SectionName).Get<DeadLetterMonitoringOptions>() ?? new DeadLetterMonitoringOptions();
		RecurringTransactionJobOptions recurringOptions = configuration.GetSection(key: RecurringTransactionJobOptions.SectionName).Get<RecurringTransactionJobOptions>() ?? new RecurringTransactionJobOptions();

		services.AddQuartz(configure: configurator =>
		{
		    configurator.AddJob<RecurringTransactionHandlingJob>(
		        configure: configure => configure.WithIdentity(name: nameof(RecurringTransactionHandlingJob), group: recurringOptions.Group)
		    );

		    configurator.AddTrigger(configure => configure
		        .ForJob(jobName: nameof(RecurringTransactionHandlingJob), jobGroup: recurringOptions.Group)
		        .WithIdentity(name: recurringOptions.TriggerName, group: recurringOptions.Group)
		        .WithCronSchedule(
		            cronExpression: recurringOptions.CronExpression,
		            schedule => schedule.InTimeZone(tz: TimeZoneInfo.Utc).WithMisfireHandlingInstructionFireAndProceed()
		        )
		    );

		    configurator.AddJob<OutboxMessagesHandlingJob>(
		        configure: configure => configure.WithIdentity(name: nameof(OutboxMessagesHandlingJob), group: outboxOptions.Group)
		    );

		    configurator.AddTrigger(configure: configure => configure
		        .ForJob(jobName: nameof(OutboxMessagesHandlingJob), jobGroup: outboxOptions.Group)
		        .WithIdentity(name: outboxOptions.TriggerName, group: outboxOptions.Group)
		        .WithSimpleSchedule(action: schedule => schedule
		            .WithIntervalInSeconds(seconds: outboxOptions.IntervalSeconds)
		            .RepeatForever()
		        )
		    );

		    configurator.AddJob<DeadLetterMonitoringJob>(
		        configure: configure => configure.WithIdentity(name: nameof(DeadLetterMonitoringJob), group: deadLetterOptions.Group)
		    );

		    configurator.AddTrigger(configure: configure => configure
		        .ForJob(jobName: nameof(DeadLetterMonitoringJob), jobGroup: deadLetterOptions.Group)
		        .WithIdentity(name: deadLetterOptions.TriggerName, group: deadLetterOptions.Group)
		        .WithSimpleSchedule(action: schedule => schedule
		            .WithIntervalInMinutes(minutes: deadLetterOptions.IntervalMinutes)
		            .RepeatForever()
		        )
		    );
		});
		
		services.AddQuartzHostedService(configure: options => options.WaitForJobsToComplete = true);
		
		return services;
	}
}