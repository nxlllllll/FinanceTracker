using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Worker.RecurringTransaction.Jobs;
using FinanceTracker.Worker.Shared.RabbitMQ;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Quartz;

namespace FinanceTracker.Worker.RecurringTransaction;

public sealed class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);

        builder.Services.AddInfrastructure(configuration: builder.Configuration);

        builder.Services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(configSectionPath: RabbitMqOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<RabbitMqConnectionFactory>();
        builder.Services.AddScoped<IRabbitMqPublisher, RabbitMqPublisher>();
        
        RecurringTransactionJobOptions recurringOptions = builder.Configuration
            .GetSection(key: RecurringTransactionJobOptions.SectionName)
            .Get<RecurringTransactionJobOptions>() ?? new RecurringTransactionJobOptions();

        builder.Services.AddQuartz(configure: q =>
        {
            q.AddJob<RecurringTransactionHandlingJob>(configure: j => j.WithIdentity(name: nameof(RecurringTransactionHandlingJob), group: recurringOptions.Group));

            q.AddTrigger(configure: t => t
                .ForJob(jobName: nameof(RecurringTransactionHandlingJob), jobGroup: recurringOptions.Group)
                .WithIdentity(name: recurringOptions.TriggerName, group: recurringOptions.Group)
                .WithCronSchedule(
                    cronExpression: recurringOptions.CronExpression,
                    schedule => schedule.InTimeZone(tz: TimeZoneInfo.Utc).WithMisfireHandlingInstructionFireAndProceed()
                )
            );
        });

        builder.Services.AddQuartzHostedService(configure: options => options.WaitForJobsToComplete = true);

        IHost app = builder.Build();
        app.Run();
    }
}